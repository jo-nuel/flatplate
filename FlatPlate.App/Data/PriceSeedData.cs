using System.Globalization;
using System.IO;
using FlatPlate.Core.Data;
using FlatPlate.Core.Enums;
using FlatPlate.Core.Models;

namespace FlatPlate.App.Data;

/// <summary>
/// Adds attributed supermarket prices for the starter ingredient catalogue.
/// </summary>
public static class PriceSeedData
{
    /// <summary>
    /// Adds a price only when the ingredient and store do not already have one.
    /// </summary>
    public static void AddMissingPrices(
        FlatPlateDbContext context,
        string? dataPath = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ingredients = context.Ingredients
            .AsEnumerable()
            .ToDictionary(
                ingredient => ingredient.Name,
                StringComparer.OrdinalIgnoreCase);
        var stores = context.Stores
            .AsEnumerable()
            .ToDictionary(
                store => store.Name,
                StringComparer.OrdinalIgnoreCase);
        var existingKeys = context.PriceEntries
            .Select(entry => new { entry.IngredientId, entry.StoreId })
            .AsEnumerable()
            .Select(entry => (entry.IngredientId, entry.StoreId))
            .ToHashSet();

        var rows = ReadRows(dataPath ?? DefaultDataPath());
        var missingPrices = new List<PriceEntry>();

        foreach (var row in rows)
        {
            if (!ingredients.TryGetValue(row.IngredientName, out var ingredient))
            {
                throw new InvalidDataException(
                    $"Price seed references unknown ingredient '{row.IngredientName}'.");
            }

            if (!stores.TryGetValue(row.StoreName, out var store))
            {
                throw new InvalidDataException(
                    $"Price seed references unknown store '{row.StoreName}'.");
            }

            if (!existingKeys.Add((ingredient.Id, store.Id)))
            {
                continue;
            }

            missingPrices.Add(new PriceEntry
            {
                IngredientId = ingredient.Id,
                StoreId = store.Id,
                Price = row.Price,
                PackSize = row.PackSize,
                LastUpdated = row.CollectedDate,
                Source = PriceSource.Seeded,
            });
        }

        if (missingPrices.Count == 0)
        {
            return;
        }

        context.PriceEntries.AddRange(missingPrices);
        context.SaveChanges();
    }

    private static string DefaultDataPath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "Seed",
            "price-seed.csv");
    }

    private static IReadOnlyList<PriceSeedRow> ReadRows(string dataPath)
    {
        if (!File.Exists(dataPath))
        {
            throw new FileNotFoundException(
                "The supermarket price seed file was not found.",
                dataPath);
        }

        return File.ReadLines(dataPath)
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(ParseRow)
            .ToList();
    }

    private static PriceSeedRow ParseRow(string line)
    {
        var fields = line.Split(',', StringSplitOptions.TrimEntries);
        if (fields.Length != 8)
        {
            throw new InvalidDataException(
                $"Expected 8 price seed columns but found {fields.Length}.");
        }

        if (!decimal.TryParse(
                fields[4],
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var price) || price <= 0)
        {
            throw new InvalidDataException(
                $"Price seed has invalid price '{fields[4]}'.");
        }

        if (!decimal.TryParse(
                fields[5],
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var packSize) || packSize <= 0)
        {
            throw new InvalidDataException(
                $"Price seed has invalid pack size '{fields[5]}'.");
        }

        if (!DateTime.TryParseExact(
                fields[6],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var collectedDate))
        {
            throw new InvalidDataException(
                $"Price seed has invalid collection date '{fields[6]}'.");
        }

        return new PriceSeedRow(
            fields[0],
            fields[1],
            price,
            packSize,
            collectedDate);
    }

    private sealed record PriceSeedRow(
        string IngredientName,
        string StoreName,
        decimal Price,
        decimal PackSize,
        DateTime CollectedDate);
}
