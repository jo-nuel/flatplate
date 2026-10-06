using FlatPlate.Core.Enums;
using FlatPlate.Core.Services;

namespace FlatPlate.Core.Models;

/// <summary>
/// Represents an ingredient whose base measurement is grams.
/// </summary>
public sealed class WeightIngredient : Ingredient
{
    public decimal? DensityGramsPerMl { get; set; }

    /// <inheritdoc />
    public override decimal ToBaseAmount(decimal amount, MeasureUnit unit)
    {
        return UnitConverter.ToGrams(amount, unit, DensityGramsPerMl);
    }
}
