using System.Globalization;

namespace FlatPlate.App.ViewModels;

/// <summary>A confirmed draft retained in this screen's memory, never a persisted price row.</summary>
public sealed class PriceBookPendingDraft
{
    public PriceBookPendingDraft(PriceEditDraft draft, string ingredientName, string storeName)
    {
        Draft = draft;
        IngredientName = ingredientName;
        StoreName = storeName;
    }

    public PriceEditDraft Draft { get; }
    public string IngredientName { get; }
    public string StoreName { get; }
    public string PriceDisplay => Draft.Price.ToString(CultureInfo.InvariantCulture);
    public string PackDisplay => $"{Draft.PackSize.ToString(CultureInfo.InvariantCulture)} {Draft.BaseUnit}";
    public string Status => "Not saved";
}
