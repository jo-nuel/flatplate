using FlatPlate.Core.Models;

namespace FlatPlate.Core.Interfaces;

/// <summary>
/// Calculates whole-pack shopping cost against a weekly budget.
/// </summary>
public interface IBudgetCalculator
{
    /// <summary>
    /// Calculates the cost of merged ingredients at the selected store.
    /// </summary>
    BudgetResult Calculate(IEnumerable<MergedItem> items, int storeId, decimal budget);
}
