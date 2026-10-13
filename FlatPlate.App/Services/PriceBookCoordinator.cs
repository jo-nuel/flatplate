using System.ComponentModel;
using System.IO;
using FlatPlate.App.ViewModels;
using FlatPlate.Core.Enums;
using FlatPlate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.App.Services;

/// <summary>Read, manual save, CSV export and confirmed batch import using host-supplied dependencies.</summary>
public sealed class PriceBookCoordinator : IDisposable
{
    private readonly PriceBookDataSource _data;
    private readonly IPriceBookInteraction _interaction;
    private readonly PriceCsvFileService _files;
    private readonly PriceBookWriteService? _writer;
    private readonly PriceCsvImportService? _importer;
    private IReadOnlyList<Ingredient> _ingredients = Array.Empty<Ingredient>();
    private IReadOnlyList<Store> _stores = Array.Empty<Store>();
    private bool _loaded;
    private bool _disposed;

    public PriceBookCoordinator(PriceBookViewModel viewModel, PriceBookDataSource data,
        IPriceBookInteraction interaction, PriceCsvFileService? files = null, PriceBookWriteService? writer = null)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
        _files = files ?? new PriceCsvFileService();
        _writer = writer;
        _importer = writer is null ? null : new PriceCsvImportService(writer.Context);
        AddPriceCommand = new RelayCommand(() => Run(Add, "Could not prepare a price draft."), () => Ready && _ingredients.Count > 0 && _stores.Count > 0);
        EditPriceCommand = new RelayCommand(() => Run(Edit, "Could not prepare an edit draft."), () => Ready && ViewModel.SelectedPrice is not null);
        ExportCsvCommand = new RelayCommand(() => Run(Export, "Export failed. Check the visible prices and destination file."), () => Ready && ViewModel.VisiblePrices.Cast<object>().Any());
        ImportCsvCommand = new RelayCommand(() => Run(PreviewImport, "Could not preview this file. Check that it is a readable UTF-8 CSV."), () => Ready);
        ReloadCommand = new RelayCommand(() => Load(), () => !_disposed);
        ViewModel.ConnectActions(this);
        ViewModel.SetSavingAvailable(writer is not null);
        ViewModel.PropertyChanged += OnViewModelChanged;
    }

    public PriceBookViewModel ViewModel { get; }
    public RelayCommand AddPriceCommand { get; }
    public RelayCommand EditPriceCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand ImportCsvCommand { get; }
    public RelayCommand ReloadCommand { get; }
    public Exception? LastError { get; private set; }
    public bool? LastSaveSucceeded { get; private set; }
    public bool? LastImportSucceeded { get; private set; }
    public event EventHandler? PriceSaved;
    private bool Ready => _loaded && !_disposed;

    public bool Load()
    {
        if (_disposed) return false;
        return Run(() =>
        {
            var ingredients = _data.LoadIngredients().ToArray();
            var stores = _data.LoadStores().ToArray();
            if (ingredients.Any(i => i is null || i.Id <= 0 || string.IsNullOrWhiteSpace(i.Name))
                || stores.Any(s => s is null || s.Id <= 0 || string.IsNullOrWhiteSpace(s.Name))
                || ingredients.GroupBy(i => i.Id).Any(g => g.Count() != 1) || stores.GroupBy(s => s.Id).Any(g => g.Count() != 1))
                throw new InvalidDataException("The ingredient or store catalogue is incomplete. Prices could not be loaded.");
            var prices = _data.LoadPrices();
            ViewModel.LoadPrices(prices);
            _ingredients = ingredients; _stores = stores;
            ViewModel.SetStores(stores);
            _loaded = true;
            ViewModel.SetMessage(_writer is null ? "Prices loaded. Add/Edit prepares drafts; saving is not connected." : "Prices loaded. Manual changes save in UTC; CSV import inserts new pairs only.");
        }, "Could not load prices. Any previously loaded rows are unchanged.");
    }

    private void Add()
    {
        if (!Load()) return;
        var model = PriceEditDialogViewModel.ForAdd(_ingredients, _stores, CurrentKeys());
        model.SaveOnConfirm = _writer is not null;
        ReceiveDraft(model, _interaction.ShowPriceDialog(model), null);
    }

    private void Edit()
    {
        var selected = ViewModel.SelectedPrice;
        if (selected is null || !Load()) return;
        var current = ViewModel.Prices.SingleOrDefault(r => r.IngredientId == selected.IngredientId && r.StoreId == selected.StoreId);
        if (current is null) throw new InvalidDataException("The selected price is no longer available. Choose another row.");
        ViewModel.SelectedPrice = current;
        var model = PriceEditDialogViewModel.ForEdit(current, _ingredients, _stores);
        model.SaveOnConfirm = _writer is not null;
        ReceiveDraft(model, _interaction.ShowPriceDialog(model), current);
    }

    private void ReceiveDraft(PriceEditDialogViewModel model, PriceEditDraft? draft, PriceBookRowViewModel? original)
    {
        LastSaveSucceeded = null;
        if (draft is null) { ViewModel.SetMessage("Price dialog cancelled. No price was changed."); return; }
        var ingredient = _ingredients.SingleOrDefault(i => i.Id == draft.IngredientId);
        var store = _stores.SingleOrDefault(s => s.Id == draft.StoreId);
        if (ingredient is null || store is null || draft.Price <= 0m || draft.PackSize <= 0m
            || draft.Mode != model.Mode || draft.Source != PriceSource.UserEntered
            || PriceBookRowViewModel.GetBaseUnit(ingredient) is not string unit || unit != draft.BaseUnit
            || (original is null && CurrentKeys().Contains((draft.IngredientId, draft.StoreId)))
            || (original is not null && (draft.IngredientId != original.IngredientId || draft.StoreId != original.StoreId
                || (draft.Price == original.Price && draft.PackSize == original.PackSize)
                || draft.OriginalSource != original.Source || draft.OriginalLastUpdated != original.LastUpdated)))
            throw new InvalidDataException("The dialog result does not match the current catalogue or selected price.");
        var pending = new PriceBookPendingDraft(draft, ingredient.Name, store.Name);
        ViewModel.RetainDraft(pending);
        if (_writer is null)
        {
            ViewModel.SetMessage("Draft prepared — not saved. It is retained below while this screen is open.");
            return;
        }
        try { _writer.Save(draft, original); LastSaveSucceeded = true; }
        catch (Exception exception) when (exception is InvalidDataException or DbUpdateException or InvalidOperationException or System.Data.Common.DbException)
        {
            LastSaveSucceeded = false; LastError = exception;
            ViewModel.SetMessage(exception is InvalidDataException ? exception.Message + " The draft is retained below, not saved."
                : "Price save failed. The draft is retained below, not saved. Refresh and retry.", true);
            return;
        }
        ViewModel.ReleaseDraft(pending);
        if (Load())
        {
            ViewModel.SelectedPrice = ViewModel.Prices.FirstOrDefault(r => r.IngredientId == draft.IngredientId && r.StoreId == draft.StoreId && ViewModel.VisiblePrices.Contains(r));
            ViewModel.SetMessage("Price saved. CSV import inserts new pairs only.");
        }
        else ViewModel.SetMessage("Price saved, but the book could not reload. Refresh before continuing.", true);
        NotifyPriceSaved();
    }

    private void NotifyPriceSaved()
    {
        try { PriceSaved?.Invoke(this, EventArgs.Empty); }
        catch (Exception exception)
        {
            LastError = exception; ViewModel.SetMessage("Price saved, but a dependent screen could not refresh.", true);
        }
    }

    private void Export()
    {
        var visible = ViewModel.VisiblePrices.Cast<PriceBookRowViewModel>().ToArray();
        if (visible.Length == 0) { ViewModel.SetMessage("No visible prices to export."); return; }
        var records = visible.Select(row =>
        {
            var ingredient = _ingredients.SingleOrDefault(i => i.Id == row.IngredientId);
            var store = _stores.SingleOrDefault(s => s.Id == row.StoreId);
            if (ingredient is null || store is null || ingredient.Name != row.IngredientName || store.Name != row.StoreName
                || _ingredients.Count(i => i.Name == ingredient.Name) != 1 || _stores.Count(s => s.Name == store.Name) != 1)
                throw new InvalidDataException("A visible price has a missing or ambiguous catalogue mapping; export cannot proceed.");
            var unit = PriceBookRowViewModel.GetBaseUnit(ingredient)
                ?? throw new InvalidDataException($"The unit of {ingredient.Name} is unknown; export cannot proceed.");
            if (row.BaseUnit != unit)
                throw new InvalidDataException($"The unit of {ingredient.Name} does not match the catalogue. Reload and review it before export.");
            return new PriceCsvRecord(row.IngredientName, row.StoreName, row.Price, row.PackSize, unit, row.LastUpdated, row.Source);
        }).ToArray();
        var text = Csv().Export(records);
        var path = _interaction.ChooseExportFile();
        if (path is null) { ViewModel.SetMessage("Export cancelled. No price data was changed."); return; }
        _files.Write(path, text);
        ViewModel.SetMessage($"Exported {records.Length} visible price records. Price data is unchanged.");
    }

    private void PreviewImport()
    {
        LastImportSucceeded = null;
        var path = _interaction.ChooseImportFile();
        if (path is null) { ViewModel.SetMessage("Import preview cancelled. Nothing was saved."); return; }
        if (!Load()) return;
        var parsed = Csv().Parse(_files.Read(path));
        var preview = new PriceCsvImportPreviewViewModel(parsed, _ingredients, _stores, CurrentKeys(), forImport: _importer is not null);
        if (_importer is not null) preview.ConnectImport(() => PersistImport(parsed));
        ViewModel.SetPreview(preview);
        ViewModel.SetMessage(_importer is null ? $"Preview only: {preview.Rows.Count} parsed records, {preview.Issues.Count} issues. Nothing was saved."
            : $"{preview.Rows.Count} parsed records, {preview.Issues.Count} issues. Nothing is saved until Confirm Import.");
        try { _interaction.ShowImportPreview(preview); }
        finally { preview.DisconnectImport(); }
        if (preview.LastImportError is not null)
            ViewModel.SetMessage(preview.ImportStatus, true);
        else if (!preview.HasImported)
            ViewModel.SetMessage(preview.Issues.Count > 0 ? "Import blocked by the listed problems. Nothing was saved." : "Import preview closed without confirmation. Nothing was saved.");
    }

    private int PersistImport(PriceCsvParseResult parsed)
    {
        if (_disposed || _importer is null) throw new InvalidOperationException("Saving is no longer connected. Nothing was saved.");
        int count;
        try { count = _importer.Import(parsed); LastImportSucceeded = true; }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or InvalidOperationException or DbUpdateException or System.Data.Common.DbException)
        {
            LastImportSucceeded = false; LastError = exception;
            ViewModel.SetMessage("Import failed. No records were saved. Review the preview problems and select the CSV again.", true);
            throw;
        }
        if (Load()) ViewModel.SetMessage($"Imported {count} price records. Existing prices were unchanged. The current store filter is preserved.");
        else ViewModel.SetMessage($"Imported {count} price records, but the book could not reload. The records are saved; refresh before continuing.", true);
        NotifyPriceSaved();
        return count;
    }

    private PriceCsvService Csv() => new(_ingredients.Select(i => i.Name), _stores.Select(s => s.Name));
    private IEnumerable<(int IngredientId, int StoreId)> CurrentKeys() => ViewModel.Prices.Select(r => (r.IngredientId, r.StoreId));

    private bool Run(Action action, string errorMessage)
    {
        if (_disposed) return false;
        try { LastError = null; action(); return true; }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or System.Data.Common.DbException)
        {
            LastError = exception;
            ViewModel.SetMessage(exception is InvalidDataException ? exception.Message : errorMessage, true);
            return false;
        }
        finally { RefreshCommands(); }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args) => RefreshCommands();
    private void RefreshCommands()
    {
        AddPriceCommand.RaiseCanExecuteChanged(); EditPriceCommand.RaiseCanExecuteChanged();
        ExportCsvCommand.RaiseCanExecuteChanged(); ImportCsvCommand.RaiseCanExecuteChanged(); ReloadCommand.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ViewModel.PropertyChanged -= OnViewModelChanged;
        ViewModel.DisconnectActions(this);
        ViewModel.SetSavingAvailable(false);
        RefreshCommands();
    }
}
