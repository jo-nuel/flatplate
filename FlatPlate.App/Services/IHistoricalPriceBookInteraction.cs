using FlatPlate.App.ViewModels;

namespace FlatPlate.App.Services;

/// <summary>Optional App UI boundary; ordinary CSV interactions remain unchanged.</summary>
public interface IHistoricalPriceBookInteraction
{
    HistoricalPriceFileSelection? ChooseHistoricalImportFiles();
    void ShowHistoricalImportPreview(HistoricalPriceImportPreviewViewModel preview);
}
