namespace FlatPlate.Core.Validation;

/// <summary>
/// Associates an authentication validation message with the affected field.
/// </summary>
public sealed record AuthenticationValidationError(string Field, string Message);
