using System.Windows;
using FlatPlate.App.ViewModels;
using FlatPlate.App.Views;
using Microsoft.Win32;

namespace FlatPlate.App.Services;

public sealed class PriceBookInteraction : IPriceBookInteraction, IHistoricalPriceBookInteraction
{
    private readonly Func<Window?> _owner;

    public PriceBookInteraction(Func<Window?> owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public PriceEditDraft? ShowPriceDialog(PriceEditDialogViewModel viewModel) =>
        PriceEditDialog.Prompt(_owner(), viewModel);

    public string? ChooseExportFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export visible Price Book rows", Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv", AddExtension = true, OverwritePrompt = true,
            FileName = "FlatPlate_Prices.csv",
        };
        return Show(dialog) ? dialog.FileName : null;
    }

    public string? ChooseImportFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import price CSV", Filter = "CSV files (*.csv)|*.csv",
            CheckFileExists = true, Multiselect = false,
        };
        return Show(dialog) ? dialog.FileName : null;
    }

    public void ShowImportPreview(PriceCsvImportPreviewViewModel preview) =>
        new PriceCsvImportPreviewWindow(preview) { Owner = _owner() }.ShowDialog();

    public HistoricalPriceFileSelection? ChooseHistoricalImportFiles()
    {
        string? Pick(string title, string filter)
        {
            var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true, Multiselect = false };
            return Show(dialog) ? dialog.FileName : null;
        }
        var csv = Pick("1 of 2 — Select historical prices (eight-column CSV)", "CSV files (*.csv)|*.csv");
        if (csv is null) return null;
        var evidence = Pick("2 of 2 — Select original historical evidence", "CSV files (*.csv)|*.csv");
        return evidence is null ? null : new HistoricalPriceFileSelection(csv, evidence);
    }

    public void ShowHistoricalImportPreview(HistoricalPriceImportPreviewViewModel preview) =>
        new HistoricalPriceImportPreviewWindow(preview) { Owner = _owner() }.ShowDialog();

    private bool Show(CommonDialog dialog)
    {
        var owner = _owner();
        return (owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) == true;
    }
}
