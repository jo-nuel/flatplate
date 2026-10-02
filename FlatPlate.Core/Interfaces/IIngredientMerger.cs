using FlatPlate.Core.Models;

namespace FlatPlate.Core.Interfaces;

/// <summary>
/// Combines repeated recipe ingredients into shopping-list totals.
/// </summary>
public interface IIngredientMerger
{
    /// <summary>
    /// Merges the ingredients required by the supplied planned meals.
    /// </summary>
    IReadOnlyList<MergedItem> Merge(IEnumerable<PlannedMeal> meals);
}
