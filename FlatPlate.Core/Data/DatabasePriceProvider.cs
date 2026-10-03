using FlatPlate.Core.Interfaces;
using FlatPlate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.Core.Data;

/// <summary>
/// Provides prices from SQLite as the temporary default until the CSV provider is available.
/// </summary>
public sealed class DatabasePriceProvider : IPriceProvider
{
    private readonly FlatPlateDbContext _context;

    /// <summary>
    /// Creates a price provider that reads from the supplied database context.
    /// </summary>
    public DatabasePriceProvider(FlatPlateDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public PriceEntry? GetPrice(int ingredientId, int storeId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ingredientId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(storeId);

        return _context.PriceEntries
            .AsNoTracking()
            .SingleOrDefault(entry =>
                entry.IngredientId == ingredientId && entry.StoreId == storeId);
    }

    /// <inheritdoc />
    public IReadOnlyList<PriceEntry> GetPricesForStore(int storeId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(storeId);

        return _context.PriceEntries
            .AsNoTracking()
            .Where(entry => entry.StoreId == storeId)
            .OrderBy(entry => entry.IngredientId)
            .ToList();
    }
}
