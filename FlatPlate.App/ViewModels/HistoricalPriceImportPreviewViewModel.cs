using System.IO;
using FlatPlate.App.Services;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.App.ViewModels;

public sealed record HistoricalPricePreviewRow(HistoricalPriceMatch Match, string ReviewStatus)
{
    public HistoricalPriceRecord Record => Match.Record;
    public string FinalHumanApproval => Match.FinalHumanApproval;
    public int? IngredientId => Match.IngredientId;
    public int? StoreId => Match.StoreId;
    public bool TechnicallyMatched => Match.TechnicallyMatched;
    public bool HasValidatedApprovalReceipt => Match.HasValidatedApprovalReceipt;
}

/// <summary>Explicit whole-batch confirmation only; this model does not approve products.</summary>
public sealed class HistoricalPriceImportPreviewViewModel : ViewModelBase
{
    private Func<int>? _save;
    private Action<int>? _afterCommit;
    private bool _acknowledged, _busy, _closed;

    public HistoricalPriceImportPreviewViewModel(HistoricalPriceImportPackage package,
        IReadOnlyList<HistoricalPriceMatch> rows, string? issue)
    {
        Package = package; Issue = issue ?? "";
        Rows = rows.Select(row =>
        {
            var item = package.Receipt.Items.SingleOrDefault(i => i.Record == row.Record);
            var status = row.HasValidatedApprovalReceipt ? row.ReviewStatus : item is null ? "Receipt mismatch"
                : !item.PersonallyReviewed ? "Pending personal review" : !item.ApprovedForImport ? "Not approved"
                : "Approved in receipt; blocked";
            return new HistoricalPricePreviewRow(row, status);
        }).ToArray();
        ConfirmImportCommand = new RelayCommand(Confirm, () => CanConfirmImport);
        CancelCommand = new RelayCommand(Close, () => !_busy);
    }

    public HistoricalPriceImportPackage Package { get; }
    public IReadOnlyList<HistoricalPricePreviewRow> Rows { get; }
    public string Issue { get; private set; }
    public string Status { get; private set; } = "Preview only. Nothing has been saved.";
    public bool HasImported { get; private set; }
    public int ImportedCount { get; private set; }
    public Exception? LastError { get; private set; }
    public RelayCommand ConfirmImportCommand { get; }
    public RelayCommand CancelCommand { get; }
    public bool Acknowledged
    {
        get => _acknowledged;
        set { _acknowledged = value; Changed(); }
    }
    public bool CanConfirmImport => !_closed && !_busy && !HasImported && Acknowledged && _save is not null
        && Issue.Length == 0 && Rows.Count == 40 && Rows.All(r => r.TechnicallyMatched && r.HasValidatedApprovalReceipt);
    public string Summary => $"{Rows.Count} historical rows · reviewer: {Package.Receipt.ReviewerName} · reviewed: {Package.Receipt.ReviewedOn:yyyy-MM-dd}";
    public event EventHandler? CloseRequested;
    internal void ConnectImport(Func<int> save, Action<int>? afterCommit = null)
    { _save = save; _afterCommit = afterCommit; Changed(); }
    public void Close()
    {
        if (_closed || _busy) return;
        _closed = true; _save = null; _afterCommit = null;
        if (!HasImported) Status = "Cancelled. No historical prices were saved.";
        Changed(); CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Confirm()
    {
        if (!CanConfirmImport) return;
        _busy = true; Status = "Validating again and saving the entire batch…"; Changed();
        try
        {
            ImportedCount = _save!(); HasImported = true;
            Status = $"Imported {ImportedCount} historical prices. The database transaction committed. Existing prices were unchanged.";
            _afterCommit?.Invoke(ImportedCount);
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or DbUpdateException or System.Data.Common.DbException or OperationCanceledException)
        {
            LastError = error; Issue = error.Message;
            Status = HasImported
                ? $"Imported {ImportedCount} historical prices. Prices were saved, but screen refresh failed. Do not import again; close and refresh."
                : "Import failed. Nothing was saved. Cancel and reselect the files after correcting the problem.";
            _save = null;
        }
        finally { _busy = false; Changed(); }
    }

    private void Changed()
    {
        foreach (var name in new[] { nameof(Acknowledged), nameof(CanConfirmImport), nameof(Issue), nameof(Status), nameof(HasImported), nameof(ImportedCount), nameof(LastError) })
            OnPropertyChanged(name);
        ConfirmImportCommand.RaiseCanExecuteChanged(); CancelCommand.RaiseCanExecuteChanged();
    }
}
