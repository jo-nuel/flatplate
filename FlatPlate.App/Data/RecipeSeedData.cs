using FlatPlate.Core.Data;
using FlatPlate.Core.Models;

namespace FlatPlate.App.Data;

/// <summary>
/// Adds Jonathan's starter recipes to a new or partially populated database.
/// </summary>
public static class RecipeSeedData
{
    /// <summary>
    /// Adds each starter recipe only when a recipe with the same name is absent.
    /// </summary>
    public static void AddMissingRecipes(FlatPlateDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var existingNames = context.Recipes
            .Select(recipe => recipe.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingRecipes = CreateRecipes()
            .Where(recipe => !existingNames.Contains(recipe.Name))
            .ToList();

        if (missingRecipes.Count == 0)
        {
            return;
        }

        context.Recipes.AddRange(missingRecipes);
        context.SaveChanges();
    }

    private static IReadOnlyList<Recipe> CreateRecipes()
    {
        return new[]
        {
            CreateRecipe("Chicken Stir-Fry", "Asian", 30),
            CreateRecipe("Spaghetti Bolognese", "Italian", 45),
            CreateRecipe("Vegetable Curry", "Indian", 40),
            CreateRecipe("Beef Tacos", "Mexican", 30),
            CreateRecipe("Lemon Herb Chicken", "Australian", 50),
            CreateRecipe("Tuna Pasta Bake", "Australian", 45),
            CreateRecipe("Vegetable Fried Rice", "Asian", 25),
            CreateRecipe("Lentil Soup", "Mediterranean", 50),
        };
    }

    private static Recipe CreateRecipe(
        string name,
        string cuisine,
        int prepMinutes)
    {
        return new Recipe
        {
            Name = name,
            Cuisine = cuisine,
            PrepMinutes = prepMinutes,
            BaseServings = 4,
        };
    }
}
