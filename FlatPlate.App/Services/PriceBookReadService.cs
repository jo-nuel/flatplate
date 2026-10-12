using FlatPlate.Core.Data;
using FlatPlate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.App.Services;

/// <summary>Loads current prices with ingredient and store details.</summary>
public sealed class PriceBookReadService
{
    private readonly FlatPlateDbContext _context;

    public PriceBookReadService(FlatPlateDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>Reads current prices without tracking them, ordered by ingredient and store.</summary>
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
