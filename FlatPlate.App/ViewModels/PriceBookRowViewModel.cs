using System.Globalization;
using FlatPlate.Core.Enums;
using FlatPlate.Core.Models;

namespace FlatPlate.App.ViewModels;

/// <summary>
/// Shows one ingredient's pack price at one supermarket in the Price Book table.
/// </summary>
public sealed class PriceBookRowViewModel : ViewModelBase
{
    /// <summary>
    /// Creates a table row from a price entry whose ingredient and store are loaded.
    /// </summary>
    public PriceBookRowViewModel(PriceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Ingredient is null || entry.Store is null)
        {
            throw new ArgumentException(
                $"Price entry for ingredient {entry.IngredientId} at store {entry.StoreId} " +
                "must have its Ingredient and Store loaded.",
                nameof(entry));
        }

        IngredientId = entry.IngredientId;
        StoreId = entry.StoreId;
        IngredientName = entry.Ingredient.Name;
        StoreName = entry.Store.Name;
        Price = entry.Price;
        PackSize = entry.PackSize;
        PackSizeDisplay = FormatPackSize(entry.PackSize, entry.Ingredient);
        LastUpdated = entry.LastUpdated;
        Source = entry.Source;
    }

    public int IngredientId { get; }

    public int StoreId { get; }

    public string IngredientName { get; }

    public string StoreName { get; }

    public decimal Price { get; }

    /// <summary>
    /// Pack size in the ingredient's base unit: grams, millilitres or pieces.
    /// </summary>
    public decimal PackSize { get; }

    public string PackSizeDisplay { get; }

    public DateTime LastUpdated { get; }

    public PriceSource Source { get; }

    private static string FormatPackSize(decimal packSize, Ingredient ingredient)
    {
        var amount = packSize.ToString("0.##", CultureInfo.InvariantCulture);

        // The unit comes only from the ingredient subtype; an unknown subtype shows no unit.
        string? unit = ingredient switch
        {
            WeightIngredient => "g",
            VolumeIngredient => "mL",
            CountIngredient => "pieces",
            _ => null,
        };

        return unit is null ? amount : $"{amount} {unit}";
    }
}
