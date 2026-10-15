using System.Windows;
using FlatPlate.App.ViewModels;

namespace FlatPlate.App.Views;

/// <summary>
/// Lets users review parsed price rows and validation issues before an import is committed.
/// </summary>
public partial class PriceCsvImportPreviewWindow : Window
{
    public PriceCsvImportPreviewWindow(PriceCsvImportPreviewViewModel preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        InitializeComponent();
        DataContext = preview;
    }

    private void OnClose(object sender, RoutedEventArgs args) => Close();

    protected override void OnClosed(EventArgs args)
    {
        if (DataContext is PriceCsvImportPreviewViewModel preview) preview.DisconnectImport();
        base.OnClosed(args);
    }
}
