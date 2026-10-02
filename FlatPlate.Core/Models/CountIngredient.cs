using FlatPlate.Core.Enums;

namespace FlatPlate.Core.Models;

/// <summary>
/// Represents an ingredient counted as whole pieces.
/// </summary>
public sealed class CountIngredient : Ingredient
{
    /// <inheritdoc />
    public override decimal ToBaseAmount(decimal amount, MeasureUnit unit)
    {
        throw new NotImplementedException("Unit conversion is added in milestone M3.");
    }
}
