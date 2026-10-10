using FlatPlate.Core.Enums;
using FlatPlate.Core.Models;
using FlatPlate.Core.Services;

namespace FlatPlate.Tests;

/// <summary>
/// Checks that planned meals become correctly scaled shopping-list totals.
/// </summary>
public sealed class IngredientMergerTests
{
    private readonly IngredientMerger _merger = new();

    /// <summary>
    /// Confirms that the same ingredient is combined after converting its units.
    /// </summary>
    [Test]
    public void Merge_SameIngredientInTwoUnits_ReturnsOneTotal()
    {
        var rice = CreateWeightIngredient(1, "Rice");
        var firstRecipe = CreateRecipe(2, (rice, 500m, MeasureUnit.Gram));
        var secondRecipe = CreateRecipe(2, (rice, 1m, MeasureUnit.Kilogram));
        var meals = new[]
        {
            CreateMeal(firstRecipe, 2, new DateOnly(2026, 10, 12)),
            CreateMeal(secondRecipe, 2, new DateOnly(2026, 10, 13)),
        };

        var result = _merger.Merge(meals);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Ingredient, Is.SameAs(rice));
            Assert.That(result[0].BaseAmount, Is.EqualTo(1500m));
        });
    }

    /// <summary>
    /// Confirms that unrelated ingredients remain separate shopping-list items.
    /// </summary>
    [Test]
    public void Merge_DifferentIngredients_ReturnsSeparateItems()
    {
        var rice = CreateWeightIngredient(1, "Rice");
        var milk = new VolumeIngredient { Id = 2, Name = "Milk" };
        var recipe = CreateRecipe(
            2,
            (rice, 300m, MeasureUnit.Gram),
            (milk, 500m, MeasureUnit.Millilitre));

        var result = _merger.Merge(new[]
        {
            CreateMeal(recipe, 2, new DateOnly(2026, 10, 12)),
        });

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Select(item => item.Ingredient.Name),
                Is.EqualTo(new[] { "Milk", "Rice" }));
        });
    }

    /// <summary>
    /// Confirms that choosing twice the base servings doubles every quantity.
    /// </summary>
    [Test]
    public void Merge_DoubleServings_DoublesIngredientAmount()
    {
        var rice = CreateWeightIngredient(1, "Rice");
        var recipe = CreateRecipe(2, (rice, 250m, MeasureUnit.Gram));

        var result = _merger.Merge(new[]
        {
            CreateMeal(recipe, 4, new DateOnly(2026, 10, 12)),
        });

        Assert.That(result.Single().BaseAmount, Is.EqualTo(500m));
    }

    /// <summary>
    /// Confirms that a week without meals produces an empty shopping list.
    /// </summary>
    [Test]
    public void Merge_EmptyPlan_ReturnsEmptyList()
    {
        var result = _merger.Merge(Array.Empty<PlannedMeal>());

        Assert.That(result, Is.Empty);
    }

    /// <summary>
    /// Confirms that the same recipe on two days contributes both quantities.
    /// </summary>
    [Test]
    public void Merge_SameRecipeOnTwoDays_AddsBothMeals()
    {
        var rice = CreateWeightIngredient(1, "Rice");
        var recipe = CreateRecipe(2, (rice, 200m, MeasureUnit.Gram));
        var meals = new[]
        {
            CreateMeal(recipe, 2, new DateOnly(2026, 10, 12)),
            CreateMeal(recipe, 2, new DateOnly(2026, 10, 13)),
        };

        var result = _merger.Merge(meals);

        Assert.That(result.Single().BaseAmount, Is.EqualTo(400m));
    }

    /// <summary>
    /// Confirms that a planned meal must have at least one serving.
    /// </summary>
    [TestCase(0)]
    [TestCase(-1)]
    public void Merge_NonPositiveMealServings_ThrowsArgumentOutOfRangeException(
        int servings)
    {
        var rice = CreateWeightIngredient(1, "Rice");
        var recipe = CreateRecipe(2, (rice, 200m, MeasureUnit.Gram));
        var meal = CreateMeal(recipe, servings, new DateOnly(2026, 10, 12));

        Assert.That(
            () => _merger.Merge(new[] { meal }),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    /// <summary>
    /// Confirms that scaling rejects a recipe without valid base servings.
    /// </summary>
    [Test]
    public void Merge_ZeroRecipeBaseServings_ThrowsArgumentOutOfRangeException()
    {
        var rice = CreateWeightIngredient(1, "Rice");
        var recipe = CreateRecipe(0, (rice, 200m, MeasureUnit.Gram));
        var meal = CreateMeal(recipe, 2, new DateOnly(2026, 10, 12));

        Assert.That(
            () => _merger.Merge(new[] { meal }),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    private static WeightIngredient CreateWeightIngredient(int id, string name)
    {
        return new WeightIngredient { Id = id, Name = name };
    }

    private static Recipe CreateRecipe(
        int baseServings,
        params (Ingredient Ingredient, decimal Amount, MeasureUnit Unit)[] ingredients)
    {
        var recipe = new Recipe
        {
            Name = "Test recipe",
            BaseServings = baseServings,
        };

        foreach (var item in ingredients)
        {
            recipe.Ingredients.Add(new RecipeIngredient
            {
                IngredientId = item.Ingredient.Id,
                Ingredient = item.Ingredient,
                Recipe = recipe,
                Amount = item.Amount,
                Unit = item.Unit,
            });
        }

        return recipe;
    }

    private static PlannedMeal CreateMeal(
        Recipe recipe,
        int servings,
        DateOnly date)
    {
        return new PlannedMeal
        {
            Date = date,
            Slot = MealSlot.Dinner,
            Recipe = recipe,
            Servings = servings,
        };
    }
}
