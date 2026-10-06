using FlatPlate.Core.Enums;
using FlatPlate.Core.Services;

namespace FlatPlate.Core.Models;

/// <summary>
/// Represents an ingredient whose base measurement is millilitres.
/// </summary>
public sealed class VolumeIngredient : Ingredient
{
    /// <inheritdoc />
    public override decimal ToBaseAmount(decimal amount, MeasureUnit unit)
    {
        return UnitConverter.ToMillilitres(amount, unit);
    }
}
