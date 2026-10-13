using FlatPlate.Core.Validation;

namespace FlatPlate.Server.Authentication;

/// <summary>
/// Carries either a successful value or a safe authentication failure.
/// </summary>
public sealed record AuthenticationResult<T>(
    T? Value,
    AuthenticationFailure Failure,
    string? Message = null,
    IReadOnlyList<AuthenticationValidationError>? ValidationErrors = null)
{
    public bool IsSuccess => Failure == AuthenticationFailure.None;

    public static AuthenticationResult<T> Success(T value) =>
        new(value, AuthenticationFailure.None);

    public static AuthenticationResult<T> Failed(
        AuthenticationFailure failure,
        string message) => new(default, failure, message);

    public static AuthenticationResult<T> Invalid(
        IReadOnlyList<AuthenticationValidationError> errors) =>
        new(default, AuthenticationFailure.InvalidRequest, ValidationErrors: errors);
}
