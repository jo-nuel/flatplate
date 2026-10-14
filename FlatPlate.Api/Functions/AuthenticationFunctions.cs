using System.Net;
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

    [Function("VerifyEmailLink")]
    public async Task<IActionResult> VerifyEmailLinkAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "auth/verify-email-link")]
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.VerifyEmailAsync(
            new VerifyEmailRequest(
                request.Query["email"].ToString(),
                request.Query["token"].ToString()),
            cancellationToken);

        var message = result.IsSuccess
            ? result.Value!.Message
            : result.Message ?? "This verification link is invalid or has expired.";

        return new ContentResult
        {
            Content = BuildVerificationPage(result.IsSuccess, message),
            ContentType = "text/html; charset=utf-8",
            StatusCode = result.IsSuccess
                ? StatusCodes.Status200OK
                : StatusCodes.Status400BadRequest
        };
    }

    [Function("RequestEmailVerification")]
    public Task<IActionResult> RequestEmailVerificationAsync(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "auth/request-email-verification")]
        HttpRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync<RequestEmailVerificationRequest, AccountMessageResponse>(
            request,
            authenticationService.RequestEmailVerificationAsync,
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

    private static string BuildVerificationPage(bool isSuccess, string message)
    {
        var safeMessage = WebUtility.HtmlEncode(message);
        var heading = isSuccess ? "Email verified" : "Verification unsuccessful";
        var accent = isSuccess ? "#2F7D57" : "#B42318";

        return $$"""
            <!doctype html>
            <html lang="en">
              <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{{heading}} | FlatPlate</title>
              </head>
              <body style="margin:0;background:#F5F8F5;font-family:Segoe UI,Arial,sans-serif;color:#173D2B">
                <main style="max-width:560px;margin:64px auto;padding:32px;background:#fff;border-radius:12px">
                  <h1 style="margin-top:0;color:{{accent}}">{{heading}}</h1>
                  <p>{{safeMessage}}</p>
                  <p>You can close this page and return to FlatPlate.</p>
                </main>
              </body>
            </html>
            """;
    }
}
