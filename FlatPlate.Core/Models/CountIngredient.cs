using FlatPlate.Core.Enums;
using FlatPlate.Core.Services;

namespace FlatPlate.Core.Models;

/// <summary>
/// Represents an ingredient counted as whole pieces.
/// </summary>
public sealed class CountIngredient : Ingredient
{
    /// <inheritdoc />
    public override decimal ToBaseAmount(decimal amount, MeasureUnit unit)
    {
        return UnitConverter.ToPieces(amount, unit);
    }
}
