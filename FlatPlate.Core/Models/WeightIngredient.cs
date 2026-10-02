using FlatPlate.Core.Enums;

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
        throw new NotImplementedException("Unit conversion is added in milestone M3.");
    }
}
