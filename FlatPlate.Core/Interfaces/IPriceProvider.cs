using FlatPlate.Core.Models;

namespace FlatPlate.Core.Interfaces;

/// <summary>
/// Supplies ingredient prices without tying callers to a database or CSV file.
/// </summary>
public interface IPriceProvider
{
    /// <summary>
    /// Gets the selected store's price for one ingredient, or null when missing.
    /// </summary>
    PriceEntry? GetPrice(int ingredientId, int storeId);

    /// <summary>
    /// Gets all known prices for a selected store.
    /// </summary>
    IReadOnlyList<PriceEntry> GetPricesForStore(int storeId);
}
