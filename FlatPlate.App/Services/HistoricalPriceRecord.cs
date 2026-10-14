using System.Globalization;
using System.IO;
using FlatPlate.Core.Enums;

namespace FlatPlate.App.Services;

/// <summary>Historical preparation values; product evidence is separate from database identity and human approval.</summary>
public sealed record HistoricalPriceRecord(
    string IngredientName,
    string StoreName,
    decimal PriceAUD,
    decimal PackSizeBase,
    string BaseUnit,
    DateOnly ObservedDate,
    string ProductId,
    bool InStockAtObservation)
{
    public PriceSource Source => PriceSource.Seeded;

    public static DateOnly ParseObservedDate(string text)
    {
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date))
            throw new InvalidDataException("ObservedDate must be an original date in yyyy-MM-dd format, without a time or timezone.");
        return date;
    }

    public void ValidateForReview()
    {
        if (string.IsNullOrWhiteSpace(IngredientName) || IngredientName != IngredientName.Trim()
            || string.IsNullOrWhiteSpace(StoreName) || StoreName != StoreName.Trim()
            || string.IsNullOrWhiteSpace(ProductId) || ProductId != ProductId.Trim())
            throw new InvalidDataException("Ingredient, store and source product ID must be nonempty exact values.");
        if (PriceAUD <= 0m || PackSizeBase <= 0m)
            throw new InvalidDataException("Historical pack price and base quantity must be positive.");
        if (BaseUnit is not ("g" or "mL" or "pieces"))
            throw new InvalidDataException("BaseUnit must be exactly g, mL or pieces.");
        if (ObservedDate == default)
            throw new InvalidDataException("An original observation date is required.");
    }

    /// <summary>Midnight encodes only the date in the existing schema; no observation time or UTC zone is asserted.</summary>
    public DateTime ToStoredLastUpdated()
    {
        ValidateForReview();
        return ObservedDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
    }
}
