using FlatPlate.Core.Data;
using FlatPlate.Core.Enums;
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

        if (missingRecipes.Count > 0)
        {
            context.Recipes.AddRange(missingRecipes);
            context.SaveChanges();
        }

        AddMissingRecipeIngredients(context);
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

    private static void AddMissingRecipeIngredients(
        FlatPlateDbContext context)
    {
        var recipes = context.Recipes
            .AsEnumerable()
            .GroupBy(recipe => recipe.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(recipe => recipe.Id).First(),
                StringComparer.OrdinalIgnoreCase);
        var ingredients = context.Ingredients
            .AsEnumerable()
            .ToDictionary(
                ingredient => ingredient.Name,
                StringComparer.OrdinalIgnoreCase);
        var existingLinks = context.RecipeIngredients
            .Select(item => new { item.RecipeId, item.IngredientId })
            .AsEnumerable()
            .Select(item => (item.RecipeId, item.IngredientId))
            .ToHashSet();

        var missingLinks = CreateRecipeIngredients()
            .Select(definition => new RecipeIngredient
            {
                RecipeId = recipes[definition.RecipeName].Id,
                IngredientId = ingredients[definition.IngredientName].Id,
                Amount = definition.Amount,
                Unit = definition.Unit,
            })
            .Where(item => !existingLinks.Contains(
                (item.RecipeId, item.IngredientId)))
            .ToList();

        if (missingLinks.Count == 0)
        {
            return;
        }

        context.RecipeIngredients.AddRange(missingLinks);
        context.SaveChanges();
    }

    private static IReadOnlyList<RecipeIngredientDefinition>
        CreateRecipeIngredients()
    {
        return new[]
        {
            Link("Chicken Stir-Fry", "Chicken Breast", 600, MeasureUnit.Gram),
            Link("Chicken Stir-Fry", "Broccoli", 300, MeasureUnit.Gram),
            Link("Chicken Stir-Fry", "Red Capsicum", 200, MeasureUnit.Gram),
            Link("Chicken Stir-Fry", "Carrots", 200, MeasureUnit.Gram),
            Link("Chicken Stir-Fry", "Soy Sauce", 60, MeasureUnit.Millilitre),
            Link("Chicken Stir-Fry", "Garlic Clove", 2, MeasureUnit.Piece),
            Link("Chicken Stir-Fry", "White Rice", 300, MeasureUnit.Gram),

            Link("Spaghetti Bolognese", "Beef Mince", 500, MeasureUnit.Gram),
            Link("Spaghetti Bolognese", "Spaghetti", 500, MeasureUnit.Gram),
            Link("Spaghetti Bolognese", "Diced Tomatoes", 800, MeasureUnit.Gram),
            Link("Spaghetti Bolognese", "Tomato Paste", 70, MeasureUnit.Gram),
            Link("Spaghetti Bolognese", "Brown Onion", 200, MeasureUnit.Gram),
            Link("Spaghetti Bolognese", "Garlic Clove", 2, MeasureUnit.Piece),
            Link("Spaghetti Bolognese", "Olive Oil", 15, MeasureUnit.Millilitre),

            Link("Vegetable Curry", "Potatoes", 500, MeasureUnit.Gram),
            Link("Vegetable Curry", "Carrots", 200, MeasureUnit.Gram),
            Link("Vegetable Curry", "Cauliflower", 400, MeasureUnit.Gram),
            Link("Vegetable Curry", "Chickpeas", 400, MeasureUnit.Gram),
            Link("Vegetable Curry", "Coconut Milk", 400, MeasureUnit.Millilitre),
            Link("Vegetable Curry", "Curry Powder", 30, MeasureUnit.Gram),
            Link("Vegetable Curry", "White Rice", 300, MeasureUnit.Gram),

            Link("Beef Tacos", "Beef Mince", 500, MeasureUnit.Gram),
            Link("Beef Tacos", "Tortilla", 8, MeasureUnit.Piece),
            Link("Beef Tacos", "Iceberg Lettuce", 1, MeasureUnit.Piece),
            Link("Beef Tacos", "Tomato", 3, MeasureUnit.Piece),
            Link("Beef Tacos", "Cheese", 150, MeasureUnit.Gram),
            Link("Beef Tacos", "Taco Seasoning", 30, MeasureUnit.Gram),

            Link("Lemon Herb Chicken", "Chicken Breast", 800, MeasureUnit.Gram),
            Link("Lemon Herb Chicken", "Lemon", 2, MeasureUnit.Piece),
            Link("Lemon Herb Chicken", "Garlic Clove", 3, MeasureUnit.Piece),
            Link("Lemon Herb Chicken", "Olive Oil", 30, MeasureUnit.Millilitre),
            Link("Lemon Herb Chicken", "Mixed Herbs", 10, MeasureUnit.Gram),
            Link("Lemon Herb Chicken", "Potatoes", 800, MeasureUnit.Gram),

            Link("Tuna Pasta Bake", "Tuna", 425, MeasureUnit.Gram),
            Link("Tuna Pasta Bake", "Pasta", 500, MeasureUnit.Gram),
            Link("Tuna Pasta Bake", "Full Cream Milk", 500, MeasureUnit.Millilitre),
            Link("Tuna Pasta Bake", "Cheese", 200, MeasureUnit.Gram),
            Link("Tuna Pasta Bake", "Frozen Peas", 200, MeasureUnit.Gram),
            Link("Tuna Pasta Bake", "Breadcrumbs", 80, MeasureUnit.Gram),

            Link("Vegetable Fried Rice", "White Rice", 400, MeasureUnit.Gram),
            Link("Vegetable Fried Rice", "Egg", 4, MeasureUnit.Piece),
            Link("Vegetable Fried Rice", "Frozen Peas", 200, MeasureUnit.Gram),
            Link("Vegetable Fried Rice", "Carrots", 150, MeasureUnit.Gram),
            Link("Vegetable Fried Rice", "Spring Onion", 4, MeasureUnit.Piece),
            Link("Vegetable Fried Rice", "Soy Sauce", 60, MeasureUnit.Millilitre),
            Link("Vegetable Fried Rice", "Olive Oil", 30, MeasureUnit.Millilitre),

            Link("Lentil Soup", "Red Lentils", 300, MeasureUnit.Gram),
            Link("Lentil Soup", "Carrots", 200, MeasureUnit.Gram),
            Link("Lentil Soup", "Celery Stalk", 3, MeasureUnit.Piece),
            Link("Lentil Soup", "Diced Tomatoes", 400, MeasureUnit.Gram),
            Link("Lentil Soup", "Brown Onion", 200, MeasureUnit.Gram),
            Link("Lentil Soup", "Vegetable Stock", 1, MeasureUnit.Litre),
            Link("Lentil Soup", "Garlic Clove", 2, MeasureUnit.Piece),
        };
    }

    private static RecipeIngredientDefinition Link(
        string recipeName,
        string ingredientName,
        decimal amount,
        MeasureUnit unit)
    {
        return new RecipeIngredientDefinition(
            recipeName,
            ingredientName,
            amount,
            unit);
    }

    private sealed record RecipeIngredientDefinition(
        string RecipeName,
        string IngredientName,
        decimal Amount,
        MeasureUnit Unit);
}
