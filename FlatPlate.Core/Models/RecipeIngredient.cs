using FlatPlate.Core.Enums;

namespace FlatPlate.Core.Models;

/// <summary>
/// Connects a recipe to one ingredient and stores the amount the recipe needs.
/// </summary>
public sealed class RecipeIngredient
{
    public int RecipeId { get; set; }

    public int IngredientId { get; set; }

    public decimal Amount { get; set; }

    public MeasureUnit Unit { get; set; }

    public Recipe Recipe { get; set; } = null!;

    public Ingredient Ingredient { get; set; } = null!;
}
