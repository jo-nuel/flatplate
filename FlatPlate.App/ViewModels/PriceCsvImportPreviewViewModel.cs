using System.Globalization;
using System.IO;
using FlatPlate.App.Services;
using FlatPlate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.App.ViewModels;

public sealed record PriceCsvPreviewRow(int ParsedRow, PriceCsvRecord Record,
    int? IngredientId, int? StoreId, bool UnitMatches, bool DuplicateInFile, bool ExistingPair, string ReviewStatus)
{
    public string PriceDisplay => Record.PriceAUD.ToString(CultureInfo.InvariantCulture);
    public string PackDisplay => $"{Record.PackSizeBase.ToString(CultureInfo.InvariantCulture)} {Record.BaseUnit}";
    public DateTime? ObservedUtc { get; init; }
    public string UtcDisplay => ObservedUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "";
}

public sealed record PriceCsvPreviewIssue(string Location, string Field, string Message);

/// <summary>Reviews the whole batch; only an explicitly connected Confirm command can request saving.</summary>
public sealed class PriceCsvImportPreviewViewModel : ViewModelBase
{
    private readonly bool _forImport;
    private Func<int>? _import;
    private bool _isImporting;
    private string _importStatus = "Nothing has been saved.";

    public PriceCsvImportPreviewViewModel(PriceCsvParseResult parsed,
        IReadOnlyList<Ingredient> ingredients, IReadOnlyList<Store> stores,
        IEnumerable<(int IngredientId, int StoreId)> currentKeys, bool forImport = false)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(currentKeys);
        _forImport = forImport;
        ConfirmImportCommand = new RelayCommand(ConfirmImport, () => CanConfirmImport);
        var names = ingredients.ToLookup(i => i.Name, StringComparer.Ordinal);
        var storeNames = stores.ToLookup(s => s.Name, StringComparer.Ordinal);
        var existing = currentKeys.ToHashSet();
        var issues = parsed.Errors.Select(e => new PriceCsvPreviewIssue($"Source line {e.LineNumber}", e.ColumnName, e.Message)).ToList();
        var rows = new List<PriceCsvPreviewRow>();

        for (var index = 0; index < parsed.Records.Count; index++)
        {
            var record = parsed.Records[index];
            var candidates = names[record.IngredientName].ToArray();
            var shops = storeNames[record.StoreName].ToArray();
            var ingredient = candidates.Length == 1 ? candidates[0] : null;
            var store = shops.Length == 1 ? shops[0] : null;
            var unit = ingredient is null ? null : PriceBookRowViewModel.GetBaseUnit(ingredient);
            var unitMatches = unit is not null && unit == record.BaseUnit;
            var rowNumber = index + 1;
            void Issue(string field, string message) => issues.Add(new PriceCsvPreviewIssue($"Parsed row {rowNumber}", field, message));
            if (ingredient is null) Issue("IngredientName", candidates.Length == 0 ? "No exact ingredient match." : "Ingredient name is ambiguous; choose a canonical mapping before any save.");
            if (store is null) Issue("StoreName", shops.Length == 0 ? "No exact store match." : "Store name is ambiguous; choose a canonical mapping before any save.");
            if (ingredient is not null && !unitMatches) Issue("BaseUnit", unit is null ? "Ingredient type has no known base unit." : $"CSV unit {record.BaseUnit} does not match ingredient unit {unit}.");
            var hasZone = PriceCsvImportTimestamp.TryUtc(record.LastUpdated, out var utc);
            if (forImport && !hasZone) Issue("LastUpdated", $"CSV observation: {record.LastUpdated}. Saving requires an explicit Z or numeric offset. No timezone will be assumed.");
            var current = ingredient is not null && store is not null && existing.Contains((ingredient.Id, store.Id));
            if (current) Issue("Duplicate", "A price already exists for this ingredient at this store. Import only inserts new pairs; the whole batch is blocked.");
            rows.Add(new PriceCsvPreviewRow(rowNumber, record, ingredient?.Id, store?.Id, unitMatches, false, current,
                ingredient is null || store is null || !unitMatches || (forImport && !hasZone) ? "Blocked" : current ? "Blocked" : forImport ? "Ready for import" : "Ready for review")
                { ObservedUtc = hasZone ? utc : null });
        }

        var duplicates = rows.Where(r => r.IngredientId.HasValue && r.StoreId.HasValue)
            .GroupBy(r => (r.IngredientId, r.StoreId)).Where(g => g.Count() > 1).SelectMany(g => g).Select(r => r.ParsedRow).ToHashSet();
        Rows = rows.Select(row =>
        {
            if (!duplicates.Contains(row.ParsedRow)) return row;
            issues.Add(new PriceCsvPreviewIssue($"Parsed row {row.ParsedRow}", "Duplicate", "This ingredient and store appear more than once in the file. The whole batch is blocked."));
            return row with { DuplicateInFile = true, ReviewStatus = "Blocked" };
        }).ToList().AsReadOnly();
        if (forImport && Rows.Count == 0 && issues.Count == 0) issues.Add(new PriceCsvPreviewIssue("Batch", "CSV", "There are no price records to insert."));
        Issues = issues.AsReadOnly();
        ParseErrorCount = parsed.Errors.Count;
    }

    public IReadOnlyList<PriceCsvPreviewRow> Rows { get; private set; }
    public IReadOnlyList<PriceCsvPreviewRow> ValidRows => Rows.Where(r => r.ReviewStatus is "Ready for review" or "Ready for import" or "Imported").ToArray();
    public IReadOnlyList<PriceCsvPreviewIssue> Issues { get; private set; }
    public int ParseErrorCount { get; }
    public int ReadyRowCount => ValidRows.Count;
    public bool IsBatchEligible => Rows.Count > 0 && Issues.Count == 0;
    public bool CanConfirmImport => _import is not null && IsBatchEligible && !_isImporting && !HasImported;
    public bool IsImporting => _isImporting;
    public bool HasImported { get; private set; }
    public int ImportedCount { get; private set; }
    public Exception? LastImportError { get; private set; }
    public string ImportStatus => _importStatus;
    public RelayCommand ConfirmImportCommand { get; }
    public string Summary => HasImported ? $"{ImportedCount} records imported. This preview cannot be imported again."
        : _forImport ? $"{Rows.Count} parsed records · {ReadyRowCount} valid new pairs · {Issues.Count} issues · {(IsBatchEligible ? "Entire batch eligible" : "Entire batch blocked")}"
        : $"{Rows.Count} parsed records · {ReadyRowCount} ready for review · {Issues.Count} issues · Read-only preview";
    public string Notice => _forImport
        ? "Insert new pairs only. Confirm Import saves the entire valid batch. Observation times require Z or an offset and are stored as UTC; their original offset text is not retained by the database. Source is supplied metadata, not independently verified provenance."
        : "Preview only. Nothing is saved. Original sources and observation times are shown for review.";

    internal void ConnectImport(Func<int> import)
    {
        _import = import ?? throw new ArgumentNullException(nameof(import));
        Changed();
    }
    internal void DisconnectImport() { _import = null; Changed(); }

    private void ConfirmImport()
    {
        // RelayCommand.Execute can be invoked directly; enforce the guard here as well.
        if (!CanConfirmImport) return;
        _isImporting = true; _importStatus = "Importing the whole batch…"; Changed();
        try
        {
            ImportedCount = _import!();
            HasImported = true;
            Rows = Rows.Select(r => r with { ReviewStatus = "Imported" }).ToArray();
            _importStatus = $"Imported {ImportedCount} records. The transaction committed successfully.";
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException or DbUpdateException or System.Data.Common.DbException)
        {
            LastImportError = exception;
            var details = exception is PriceCsvImportValidationException validation ? validation.Issues
                : new[] { new PriceCsvPreviewIssue("Batch", "Save", exception.Message) };
            Issues = Issues.Concat(details).ToArray();
            _importStatus = "Import failed. No records were saved. Close and select the CSV again after reviewing the problems.";
        }
        finally { _isImporting = false; Changed(); }
    }

    private void Changed()
    {
        foreach (var name in new[] { nameof(Rows), nameof(ValidRows), nameof(Issues), nameof(ReadyRowCount), nameof(IsBatchEligible), nameof(CanConfirmImport), nameof(IsImporting), nameof(HasImported), nameof(ImportedCount), nameof(ImportStatus), nameof(Summary), nameof(LastImportError) })
            OnPropertyChanged(name);
        ConfirmImportCommand.RaiseCanExecuteChanged();
    }
}
