using System.Windows;
using FlatPlate.App.ViewModels;

namespace FlatPlate.App.Views;

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
