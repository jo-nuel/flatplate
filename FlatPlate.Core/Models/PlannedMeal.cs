using FlatPlate.Core.Enums;

namespace FlatPlate.Core.Models;

/// <summary>
/// Places a recipe into one meal slot on a specific day.
/// </summary>
public sealed class PlannedMeal
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public MealSlot Slot { get; set; }

    public int RecipeId { get; set; }

    public int Servings { get; set; }

    public Recipe Recipe { get; set; } = null!;
}
