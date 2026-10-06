using FlatPlate.Core.Enums;
using FlatPlate.Core.Exceptions;

namespace FlatPlate.Core.Services;

/// <summary>
/// Converts recipe measurements to grams, millilitres, or pieces before they
/// are merged into a shopping list.
/// </summary>
public static class UnitConverter
{
    private static readonly IReadOnlyDictionary<MeasureUnit, decimal> WeightToGrams =
        new Dictionary<MeasureUnit, decimal>
        {
            [MeasureUnit.Gram] = 1m,
            [MeasureUnit.Kilogram] = 1000m,
        };

    private static readonly IReadOnlyDictionary<MeasureUnit, decimal> VolumeToMillilitres =
        new Dictionary<MeasureUnit, decimal>
        {
            [MeasureUnit.Millilitre] = 1m,
            [MeasureUnit.Litre] = 1000m,
            [MeasureUnit.Teaspoon] = 5m,
            [MeasureUnit.Tablespoon] = 20m,
            [MeasureUnit.Cup] = 250m,
        };

    /// <summary>
    /// Converts a weight measurement to grams. A volume can also be converted
    /// when the ingredient has a known density in grams per millilitre.
    /// </summary>
    public static decimal ToGrams(
        decimal amount,
        MeasureUnit unit,
        decimal? densityGramsPerMl = null)
    {
        ValidateAmount(amount);

        if (WeightToGrams.TryGetValue(unit, out var weightFactor))
        {
            return amount * weightFactor;
        }

        if (VolumeToMillilitres.TryGetValue(unit, out var volumeFactor))
        {
            ValidateDensity(densityGramsPerMl);
            return amount * volumeFactor * densityGramsPerMl!.Value;
        }

        throw new UnitConversionException($"Cannot convert {unit} to grams.");
    }

    /// <summary>
    /// Converts an Australian kitchen volume measurement to millilitres.
    /// </summary>
    public static decimal ToMillilitres(decimal amount, MeasureUnit unit)
    {
        ValidateAmount(amount);

        if (VolumeToMillilitres.TryGetValue(unit, out var factor))
        {
            return amount * factor;
        }

        throw new UnitConversionException($"Cannot convert {unit} to millilitres.");
    }

    /// <summary>
    /// Returns a count measurement in pieces.
    /// </summary>
    public static decimal ToPieces(decimal amount, MeasureUnit unit)
    {
        ValidateAmount(amount);

        if (unit == MeasureUnit.Piece)
        {
            return amount;
        }

        throw new UnitConversionException($"Cannot convert {unit} to pieces.");
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Ingredient amount must be greater than zero.");
        }
    }

    private static void ValidateDensity(decimal? densityGramsPerMl)
    {
        if (densityGramsPerMl is null)
        {
            throw new UnitConversionException(
                "A density is required to convert volume to grams.");
        }

        if (densityGramsPerMl <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(densityGramsPerMl),
                densityGramsPerMl,
                "Ingredient density must be greater than zero.");
        }
    }
}
