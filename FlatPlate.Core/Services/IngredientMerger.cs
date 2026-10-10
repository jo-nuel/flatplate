using FlatPlate.Core.Interfaces;
using FlatPlate.Core.Models;

namespace FlatPlate.Core.Services;

/// <summary>
/// Combines repeated recipe ingredients into one total for the shopping list.
/// </summary>
public sealed class IngredientMerger : IIngredientMerger
{
    /// <inheritdoc />
    public IReadOnlyList<MergedItem> Merge(IEnumerable<PlannedMeal> meals)
    {
        ArgumentNullException.ThrowIfNull(meals);

        return meals
            .SelectMany(GetScaledIngredients)
            .GroupBy(item => item.Ingredient.Id)
            .Select(group => new MergedItem
            {
                Ingredient = group.First().Ingredient,
                BaseAmount = group.Sum(item => item.BaseAmount),
            })
            .OrderBy(item => item.Ingredient.Name)
            .ToList();
    }

    private static IEnumerable<MergedItem> GetScaledIngredients(PlannedMeal meal)
    {
        ArgumentNullException.ThrowIfNull(meal);

        if (meal.Servings <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(meal),
                "Planned meal servings must be greater than zero.");
        }

        if (meal.Recipe.BaseServings <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(meal),
                "Recipe base servings must be greater than zero.");
        }

        var servingScale = (decimal)meal.Servings / meal.Recipe.BaseServings;

        return meal.Recipe.Ingredients.Select(recipeIngredient => new MergedItem
        {
            Ingredient = recipeIngredient.Ingredient,
            BaseAmount = recipeIngredient.Ingredient.ToBaseAmount(
                recipeIngredient.Amount,
                recipeIngredient.Unit) * servingScale,
        });
    }
}
