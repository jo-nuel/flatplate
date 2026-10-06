namespace FlatPlate.Core.Exceptions;

/// <summary>
/// Reports that an ingredient cannot use the requested measurement unit.
/// </summary>
public sealed class UnitConversionException : ArgumentException
{
    /// <summary>
    /// Creates an error with a plain explanation of the incompatible unit.
    /// </summary>
    public UnitConversionException(string message)
        : base(message)
    {
    }
}
