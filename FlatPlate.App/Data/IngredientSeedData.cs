using FlatPlate.Core.Data;
using FlatPlate.Core.Enums;
using FlatPlate.Core.Models;

namespace FlatPlate.App.Data;

/// <summary>
/// Adds the reusable ingredient catalogue required by the starter recipes.
/// </summary>
public static class IngredientSeedData
{
    /// <summary>
    /// Adds each ingredient only when an ingredient with the same name is absent.
    /// </summary>
    public static void AddMissingIngredients(FlatPlateDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var existingNames = context.Ingredients
            .Select(ingredient => ingredient.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingIngredients = CreateIngredients()
            .Where(ingredient => !existingNames.Contains(ingredient.Name))
            .ToList();

        if (missingIngredients.Count == 0)
        {
            return;
        }

        context.Ingredients.AddRange(missingIngredients);
        context.SaveChanges();
    }

    private static IReadOnlyList<Ingredient> CreateIngredients()
    {
        return new Ingredient[]
        {
            Weight("Chicken Breast", Aisle.Meat),
            Weight("Beef Mince", Aisle.Meat),
            Weight("Tuna", Aisle.Seafood),
            Weight("Broccoli", Aisle.Produce),
            Weight("Red Capsicum", Aisle.Produce),
            Weight("Carrots", Aisle.Produce),
            Weight("Brown Onion", Aisle.Produce),
            Weight("Potatoes", Aisle.Produce),
            Weight("Cauliflower", Aisle.Produce),
            Weight("Cheese", Aisle.Dairy),
            Weight("White Rice", Aisle.Pantry),
            Weight("Spaghetti", Aisle.Pantry),
            Weight("Pasta", Aisle.Pantry),
            Weight("Diced Tomatoes", Aisle.Pantry),
            Weight("Tomato Paste", Aisle.Pantry),
            Weight("Chickpeas", Aisle.Pantry),
            Weight("Frozen Peas", Aisle.Frozen),
            Weight("Breadcrumbs", Aisle.Pantry),
            Weight("Red Lentils", Aisle.Pantry),
            Weight("Curry Powder", Aisle.Pantry),
            Weight("Taco Seasoning", Aisle.Pantry),
            Weight("Mixed Herbs", Aisle.Pantry),
            Volume("Soy Sauce", Aisle.Pantry),
            Volume("Olive Oil", Aisle.Pantry),
            Volume("Coconut Milk", Aisle.Pantry),
            Volume("Full Cream Milk", Aisle.Dairy),
            Volume("Vegetable Stock", Aisle.Pantry),
            Count("Garlic Clove", Aisle.Produce),
            Count("Tortilla", Aisle.Bakery),
            Count("Iceberg Lettuce", Aisle.Produce),
            Count("Tomato", Aisle.Produce),
            Count("Lemon", Aisle.Produce),
            Count("Egg", Aisle.Dairy),
            Count("Spring Onion", Aisle.Produce),
            Count("Celery Stalk", Aisle.Produce),
        };
    }

    private static WeightIngredient Weight(string name, Aisle aisle)
    {
        return new WeightIngredient { Name = name, Aisle = aisle };
    }

    private static VolumeIngredient Volume(string name, Aisle aisle)
    {
        return new VolumeIngredient { Name = name, Aisle = aisle };
    }

    private static CountIngredient Count(string name, Aisle aisle)
    {
        return new CountIngredient { Name = name, Aisle = aisle };
    }
}
