using System.Globalization;
using FlatPlate.Core.Enums;

namespace FlatPlate.App.Services;

/// <summary>Holds the seven CSV V1 fields without database IDs or inferred time zones.</summary>
public sealed record PriceCsvRecord(
    string IngredientName,
    string StoreName,
    decimal PriceAUD,
    decimal PackSizeBase,
    string BaseUnit,
    string LastUpdated,
    PriceSource Source)
{
    /// <summary>Formats the supplied DateTime using its existing Kind and round-trip format.</summary>
    public PriceCsvRecord(
        string ingredientName,
        string storeName,
        decimal priceAUD,
        decimal packSizeBase,
        string baseUnit,
        DateTime lastUpdated,
        PriceSource source)
        : this(ingredientName, storeName, priceAUD, packSizeBase, baseUnit,
            lastUpdated.ToString("O", CultureInfo.InvariantCulture), source)
    {
    }

    /// <summary>Preserves the supplied explicit offset in round-trip format.</summary>
    public PriceCsvRecord(
        string ingredientName,
        string storeName,
        decimal priceAUD,
        decimal packSizeBase,
        string baseUnit,
        DateTimeOffset lastUpdated,
        PriceSource source)
        : this(ingredientName, storeName, priceAUD, packSizeBase, baseUnit,
            lastUpdated.ToString("O", CultureInfo.InvariantCulture), source)
    {
    }
}
