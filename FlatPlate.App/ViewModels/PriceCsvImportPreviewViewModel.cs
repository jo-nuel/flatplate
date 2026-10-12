using System.Globalization;
using FlatPlate.App.Services;
using FlatPlate.Core.Models;

namespace FlatPlate.App.ViewModels;

public sealed record PriceCsvPreviewRow(int ParsedRow, PriceCsvRecord Record,
    int? IngredientId, int? StoreId, bool UnitMatches, bool DuplicateInFile, bool ExistingPair, string ReviewStatus)
{
    public string PriceDisplay => Record.PriceAUD.ToString(CultureInfo.InvariantCulture);
    public string PackDisplay => $"{Record.PackSizeBase.ToString(CultureInfo.InvariantCulture)} {Record.BaseUnit}";
}

public sealed record PriceCsvPreviewIssue(string Location, string Field, string Message);

/// <summary>Static, non-persistent review of parsed records and their catalogue mappings.</summary>
public sealed class PriceCsvImportPreviewViewModel
{
    public PriceCsvImportPreviewViewModel(PriceCsvParseResult parsed,
        IReadOnlyList<Ingredient> ingredients, IReadOnlyList<Store> stores,
        IEnumerable<(int IngredientId, int StoreId)> currentKeys)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(currentKeys);
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
            var current = ingredient is not null && store is not null && existing.Contains((ingredient.Id, store.Id));
            if (current) Issue("Duplicate", "A price already exists for this ingredient at this store. Review needed; nothing is saved.");
            rows.Add(new PriceCsvPreviewRow(rowNumber, record, ingredient?.Id, store?.Id, unitMatches, false, current,
                ingredient is null || store is null || !unitMatches ? "Blocked" : current ? "Needs decision" : "Ready for review"));
        }

        var duplicates = rows.Where(r => r.IngredientId.HasValue && r.StoreId.HasValue)
            .GroupBy(r => (r.IngredientId, r.StoreId)).Where(g => g.Count() > 1).SelectMany(g => g).Select(r => r.ParsedRow).ToHashSet();
        Rows = rows.Select(row =>
        {
            if (!duplicates.Contains(row.ParsedRow)) return row;
            issues.Add(new PriceCsvPreviewIssue($"Parsed row {row.ParsedRow}", "Duplicate", "This ingredient and store appear more than once in the file. Review which record to use."));
            return row with { DuplicateInFile = true, ReviewStatus = row.ReviewStatus == "Blocked" ? "Blocked" : "Needs decision" };
        }).ToList().AsReadOnly();
        Issues = issues.AsReadOnly();
        ParseErrorCount = parsed.Errors.Count;
    }

    public IReadOnlyList<PriceCsvPreviewRow> Rows { get; }
    public IReadOnlyList<PriceCsvPreviewIssue> Issues { get; }
    public int ParseErrorCount { get; }
    public int ReadyRowCount => Rows.Count(r => r.ReviewStatus == "Ready for review");
    public string Summary => $"{Rows.Count} parsed records · {ReadyRowCount} ready for review · {Issues.Count} issues";
    public string Notice => "Preview only. Nothing is saved. Original sources and observation times are shown for review.";
}
