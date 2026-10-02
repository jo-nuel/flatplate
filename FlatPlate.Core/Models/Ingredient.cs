using FlatPlate.Core.Enums;

namespace FlatPlate.Core.Models;

/// <summary>
/// Describes a food item that recipes use and shops sell.
/// </summary>
public abstract class Ingredient
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Aisle Aisle { get; set; }

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();

    public ICollection<PriceEntry> Prices { get; set; } = new List<PriceEntry>();

    /// <summary>
    /// Converts a recipe amount to grams, millilitres, or pieces for merging.
    /// </summary>
    public abstract decimal ToBaseAmount(decimal amount, MeasureUnit unit);
}
