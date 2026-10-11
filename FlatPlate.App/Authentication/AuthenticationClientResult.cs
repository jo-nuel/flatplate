using FlatPlate.Core.Validation;

namespace FlatPlate.App.Authentication;

/// <summary>
/// Represents either a successful API response or client-safe failure details.
/// </summary>
public sealed record AuthenticationClientResult<T>(
    T? Value,
    AuthenticationClientFailure Failure,
    string? Message = null,
    IReadOnlyList<AuthenticationValidationError>? ValidationErrors = null)
{
    public bool IsSuccess => Failure == AuthenticationClientFailure.None;

    public static AuthenticationClientResult<T> Success(T value) =>
        new(value, AuthenticationClientFailure.None);

    public static AuthenticationClientResult<T> Failed(
        AuthenticationClientFailure failure,
        string message,
        IReadOnlyList<AuthenticationValidationError>? validationErrors = null) =>
        new(default, failure, message, validationErrors);
}
