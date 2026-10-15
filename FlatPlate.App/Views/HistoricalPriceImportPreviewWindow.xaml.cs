using System.Windows;
using FlatPlate.App.ViewModels;

namespace FlatPlate.App.Views;

/// <summary>
/// Displays the evidence checks that must pass before historical prices can be imported.
/// </summary>
public partial class HistoricalPriceImportPreviewWindow : Window
{
    private readonly HistoricalPriceImportPreviewViewModel _preview;
    public HistoricalPriceImportPreviewWindow(HistoricalPriceImportPreviewViewModel preview)
    {
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        InitializeComponent(); DataContext = preview;
        _preview.CloseRequested += OnCloseRequested;
    }
    private void OnCloseRequested(object? sender, EventArgs args) => Close();
    protected override void OnClosed(EventArgs args)
    {
        _preview.CloseRequested -= OnCloseRequested;
        _preview.Close();
        base.OnClosed(args);
    }
}
