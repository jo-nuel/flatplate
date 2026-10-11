using FlatPlate.Core.Data;
using FlatPlate.Core.Models;

namespace FlatPlate.App.Data;

/// <summary>
/// Adds the supermarkets available for price selection.
/// </summary>
public static class StoreSeedData
{
    /// <summary>
    /// Adds each supported store only when a store with the same name is absent.
    /// </summary>
    public static void AddMissingStores(FlatPlateDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var existingNames = context.Stores
            .Select(store => store.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingStores = CreateStores()
            .Where(store => !existingNames.Contains(store.Name))
            .ToList();

        if (missingStores.Count == 0)
        {
            return;
        }

        context.Stores.AddRange(missingStores);
        context.SaveChanges();
    }

    private static IReadOnlyList<Store> CreateStores()
    {
        return new[]
        {
            new Store { Name = "Woolworths" },
            new Store { Name = "Coles" },
        };
    }
}
