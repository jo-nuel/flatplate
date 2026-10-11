using FlatPlate.Core.Data;
using FlatPlate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.App.Services;

/// <summary>
/// Loads current pack prices and their related display data for the Price Book.
/// </summary>
public sealed class PriceBookReadService
{
    private readonly FlatPlateDbContext _context;

    /// <summary>
    /// Creates a read service using the supplied database context.
    /// </summary>
    public PriceBookReadService(FlatPlateDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>
    /// Reads current prices without tracking them, ordered by ingredient and store.
    /// </summary>
    public IReadOnlyList<PriceEntry> LoadAllCurrentPrices()
    {
        return _context.PriceEntries
            .AsNoTracking()
            .Include(entry => entry.Ingredient)
            .Include(entry => entry.Store)
            .OrderBy(entry => entry.Ingredient.Name)
            .ThenBy(entry => entry.Store.Name)
            .ThenBy(entry => entry.IngredientId)
            .ThenBy(entry => entry.StoreId)
            .ToList();
    }
}
