using System.Text.Json;
using FlatPlate.Core.Contracts.Authentication;
using FlatPlate.Server.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace FlatPlate.Api.Functions;

/// <summary>
/// Exposes the account lifecycle to the desktop client over HTTPS.
/// </summary>
public sealed class AuthenticationFunctions(IAuthenticationService authenticationService)
{
    [Function("Register")]
    public Task<IActionResult> RegisterAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/register")]
        HttpRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<RegisterRequest, AccountMessageResponse>(
            request,
            authenticationService.RegisterAsync,
            cancellationToken);

    [Function("VerifyEmail")]
    public Task<IActionResult> VerifyEmailAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/verify-email")]
        HttpRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<VerifyEmailRequest, AccountMessageResponse>(
            request,
            authenticationService.VerifyEmailAsync,
            cancellationToken);

    [Function("SignIn")]
    public Task<IActionResult> SignInAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/sign-in")]
        HttpRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<SignInRequest, AuthenticationResponse>(
            request,
            authenticationService.SignInAsync,
            cancellationToken);

    [Function("RequestPasswordReset")]
    public Task<IActionResult> RequestPasswordResetAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "auth/request-password-reset")]
        HttpRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<RequestPasswordResetRequest, AccountMessageResponse>(
            request,
            authenticationService.RequestPasswordResetAsync,
            cancellationToken);

    [Function("ResetPassword")]
    public Task<IActionResult> ResetPasswordAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/reset-password")]
        HttpRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<ResetPasswordRequest, AccountMessageResponse>(
            request,
            authenticationService.ResetPasswordAsync,
            cancellationToken);

    private static async Task<IActionResult> ExecuteAsync<TRequest, TResponse>(
        HttpRequest request,
        Func<TRequest, CancellationToken, Task<AuthenticationResult<TResponse>>> operation,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        TRequest? body;
        try
        {
            body = await request.ReadFromJsonAsync<TRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new { Message = "The request body is invalid." });
        }

        if (body is null)
        {
            return new BadRequestObjectResult(new { Message = "A request body is required." });
        }

        var result = await operation(body, cancellationToken);
        if (result.IsSuccess)
        {
            return new OkObjectResult(result.Value);
        }

        var error = new
        {
            result.Message,
            Errors = result.ValidationErrors
        };

        return result.Failure switch
        {
            AuthenticationFailure.DuplicateEmail => new ConflictObjectResult(error),
            AuthenticationFailure.InvalidCredentials => new UnauthorizedObjectResult(error),
            AuthenticationFailure.EmailNotVerified => new ObjectResult(error)
            {
                StatusCode = StatusCodes.Status403Forbidden
            },
            _ => new BadRequestObjectResult(error)
        };
    }
}
