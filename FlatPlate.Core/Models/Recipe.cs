namespace FlatPlate.Core.Models;

/// <summary>
/// Describes a meal and the ingredient quantities needed for its base servings.
/// </summary>
public sealed class Recipe
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Cuisine { get; set; } = string.Empty;

    public int PrepMinutes { get; set; }

    public int BaseServings { get; set; }

    public string? ImagePath { get; set; }

    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();

    public ICollection<PlannedMeal> PlannedMeals { get; set; } = new List<PlannedMeal>();
}
