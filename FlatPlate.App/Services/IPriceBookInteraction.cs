using FlatPlate.App.ViewModels;

namespace FlatPlate.App.Services;

/// <summary>App-owned UI boundary, separate from database access and CSV processing.</summary>
public interface IPriceBookInteraction
{
    PriceEditDraft? ShowPriceDialog(PriceEditDialogViewModel viewModel);
    string? ChooseExportFile();
    string? ChooseImportFile();
    void ShowImportPreview(PriceCsvImportPreviewViewModel preview);
}
