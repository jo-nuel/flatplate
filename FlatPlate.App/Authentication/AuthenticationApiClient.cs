using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FlatPlate.Core.Contracts.Authentication;
using FlatPlate.Core.Validation;

namespace FlatPlate.App.Authentication;

/// <summary>
/// Calls the FlatPlate authentication endpoints and converts HTTP failures into UI results.
/// </summary>
public sealed class AuthenticationApiClient : IAuthenticationApiClient
{
    private readonly HttpClient _httpClient;

    public AuthenticationApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ValidateBaseAddress(httpClient.BaseAddress);
    }

    public Task<AuthenticationClientResult<AccountMessageResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<RegisterRequest, AccountMessageResponse>(
            "auth/register",
            request,
            cancellationToken);

    public Task<AuthenticationClientResult<AccountMessageResponse>> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<VerifyEmailRequest, AccountMessageResponse>(
            "auth/verify-email",
            request,
            cancellationToken);

    public Task<AuthenticationClientResult<AuthenticationResponse>> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SignInRequest, AuthenticationResponse>(
            "auth/sign-in",
            request,
            cancellationToken);

    public Task<AuthenticationClientResult<AccountMessageResponse>> RequestPasswordResetAsync(
        RequestPasswordResetRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<RequestPasswordResetRequest, AccountMessageResponse>(
            "auth/request-password-reset",
            request,
            cancellationToken);

    public Task<AuthenticationClientResult<AccountMessageResponse>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<ResetPasswordRequest, AccountMessageResponse>(
            "auth/reset-password",
            request,
            cancellationToken);

    private async Task<AuthenticationClientResult<TResponse>> PostAsync<TRequest, TResponse>(
        string route,
        TRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                route,
                request,
                cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var value = await response.Content.ReadFromJsonAsync<TResponse>(
                    cancellationToken: cancellationToken);
                return value is null
                    ? AuthenticationClientResult<TResponse>.Failed(
                        AuthenticationClientFailure.InvalidResponse,
                        "The server returned an empty response.")
                    : AuthenticationClientResult<TResponse>.Success(value);
            }

            var error = await ReadErrorAsync(response, cancellationToken);
            return AuthenticationClientResult<TResponse>.Failed(
                MapFailure(response.StatusCode, error),
                error.Message ?? DefaultMessage(response.StatusCode),
                error.Errors);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AuthenticationClientResult<TResponse>.Failed(
                AuthenticationClientFailure.Network,
                "The server took too long to respond. Try again.");
        }
        catch (HttpRequestException)
        {
            return AuthenticationClientResult<TResponse>.Failed(
                AuthenticationClientFailure.Network,
                "FlatPlate could not reach the account service. Check your connection.");
        }
        catch (JsonException)
        {
            return AuthenticationClientResult<TResponse>.Failed(
                AuthenticationClientFailure.InvalidResponse,
                "The server returned a response FlatPlate could not read.");
        }
    }

    private static async Task<AuthenticationErrorResponse> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<AuthenticationErrorResponse>(
                cancellationToken: cancellationToken)
                ?? new AuthenticationErrorResponse();
        }
        catch (JsonException)
        {
            return new AuthenticationErrorResponse();
        }
    }

    private static AuthenticationClientFailure MapFailure(
        HttpStatusCode statusCode,
        AuthenticationErrorResponse error)
    {
        if (statusCode == HttpStatusCode.BadRequest && error.Errors?.Count > 0)
        {
            return AuthenticationClientFailure.InvalidRequest;
        }

        return statusCode switch
        {
            HttpStatusCode.BadRequest => AuthenticationClientFailure.InvalidOrExpiredCode,
            HttpStatusCode.Unauthorized => AuthenticationClientFailure.InvalidCredentials,
            HttpStatusCode.Forbidden => AuthenticationClientFailure.EmailNotVerified,
            HttpStatusCode.Conflict => AuthenticationClientFailure.DuplicateEmail,
            _ when (int)statusCode >= 500 => AuthenticationClientFailure.Server,
            _ => AuthenticationClientFailure.InvalidResponse
        };
    }

    private static string DefaultMessage(HttpStatusCode statusCode) =>
        (int)statusCode >= 500
            ? "The account service is temporarily unavailable. Try again later."
            : "The account request could not be completed.";

    private static void ValidateBaseAddress(Uri? baseAddress)
    {
        if (baseAddress is null || !baseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException(
                "The authentication client requires an absolute API base address.");
        }

        var isSecure = baseAddress.Scheme == Uri.UriSchemeHttps;
        var isLocalDevelopment = baseAddress.IsLoopback
            && baseAddress.Scheme == Uri.UriSchemeHttp;
        if (!isSecure && !isLocalDevelopment)
        {
            throw new ArgumentException(
                "The authentication API must use HTTPS outside local development.");
        }
    }

    private sealed record AuthenticationErrorResponse(
        string? Message = null,
        IReadOnlyList<AuthenticationValidationError>? Errors = null);
}
