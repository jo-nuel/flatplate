using FlatPlate.Core.Enums;

namespace FlatPlate.Core.Models;

/// <summary>
/// Stores the pack price and pack size for one ingredient at one supermarket.
/// </summary>
public sealed class PriceEntry
{
    public int IngredientId { get; set; }

    public int StoreId { get; set; }

    public decimal Price { get; set; }

    public decimal PackSize { get; set; }

    public DateTime LastUpdated { get; set; }

    public PriceSource Source { get; set; }

    public Ingredient Ingredient { get; set; } = null!;

    public Store Store { get; set; } = null!;
}
