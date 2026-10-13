using System.Net;
using System.Text;
using FlatPlate.App.Authentication;
using FlatPlate.Core.Contracts.Authentication;

namespace FlatPlate.Tests;

/// <summary>
/// Checks authentication HTTP mapping and process-only session behavior.
/// </summary>
public sealed class AuthenticationApiClientTests
{
    [Test]
    public async Task SignInAsync_SuccessfulResponse_ReturnsAuthenticationDetails()
    {
        var userId = Guid.NewGuid();
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo("/api/auth/sign-in"));
            return JsonResponse(
                HttpStatusCode.OK,
                $$"""
                {
                  "accessToken": "signed-token",
                  "expiresAtUtc": "2026-10-11T12:15:00+00:00",
                  "user": {
                    "id": "{{userId}}",
                    "displayName": "Jonathan",
                    "email": "jonathan@example.com",
                    "isEmailVerified": true
                  }
                }
                """);
        });
        using var httpClient = CreateHttpClient(handler);
        var client = new AuthenticationApiClient(httpClient);

        var result = await client.SignInAsync(new(
            "jonathan@example.com",
            "FlatPlate123"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.AccessToken, Is.EqualTo("signed-token"));
            Assert.That(result.Value.User.Id, Is.EqualTo(userId));
        });
    }

    [Test]
    public async Task RegisterAsync_Conflict_MapsDuplicateEmailFailure()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.Conflict,
            """
            { "message": "An account already uses that email address." }
            """));
        using var httpClient = CreateHttpClient(handler);
        var client = new AuthenticationApiClient(httpClient);

        var result = await client.RegisterAsync(new(
            "Jonathan",
            "jonathan@example.com",
            "FlatPlate123",
            "FlatPlate123"));

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Failure,
                Is.EqualTo(AuthenticationClientFailure.DuplicateEmail));
            Assert.That(result.Message, Does.Contain("already uses"));
        });
    }

    [Test]
    public async Task VerifyEmailAsync_ValidationResponse_PreservesFieldErrors()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            HttpStatusCode.BadRequest,
            """
            {
              "errors": [
                { "field": "Token", "message": "Enter the verification code." }
              ]
            }
            """));
        using var httpClient = CreateHttpClient(handler);
        var client = new AuthenticationApiClient(httpClient);

        var result = await client.VerifyEmailAsync(new(
            "jonathan@example.com",
            ""));

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Failure,
                Is.EqualTo(AuthenticationClientFailure.InvalidRequest));
            Assert.That(result.ValidationErrors!.Single().Field, Is.EqualTo("Token"));
        });
    }

    [Test]
    public async Task RequestEmailVerificationAsync_UsesResendEndpoint()
    {
        string? requestedPath = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedPath = request.RequestUri?.AbsolutePath;
            return JsonResponse(
                HttpStatusCode.OK,
                """{ "message": "A new code has been sent." }""");
        });
        using var httpClient = CreateHttpClient(handler);
        var client = new AuthenticationApiClient(httpClient);

        var result = await client.RequestEmailVerificationAsync(new(
            "jonathan@example.com"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(
                requestedPath,
                Is.EqualTo("/api/auth/request-email-verification"));
        });
    }

    [Test]
    public async Task SignInAsync_UnreachableServer_ReturnsNetworkFailure()
    {
        var handler = new StubHttpMessageHandler(
            _ => throw new HttpRequestException("Test network failure."));
        using var httpClient = CreateHttpClient(handler);
        var client = new AuthenticationApiClient(httpClient);

        var result = await client.SignInAsync(new(
            "jonathan@example.com",
            "FlatPlate123"));

        Assert.That(result.Failure, Is.EqualTo(AuthenticationClientFailure.Network));
    }

    [Test]
    public void Constructor_RemoteHttpAddress_RejectsInsecureTransport()
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://example.com/api/")
        };

        Assert.That(
            () => new AuthenticationApiClient(httpClient),
            Throws.ArgumentException.With.Message.Contains("HTTPS"));
    }

    [Test]
    public void Session_ExpiredToken_IsClearedAndRaisesChangeEvent()
    {
        var clock = new TestTimeProvider(
            new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        var session = new AuthenticationSession(clock);
        var changes = 0;
        session.SessionChanged += (_, _) => changes++;
        session.Start(CreateAuthenticationResponse(clock.GetUtcNow().AddMinutes(15)));
        clock.Advance(TimeSpan.FromMinutes(16));

        var hasToken = session.TryGetAccessToken(out var token);

        Assert.Multiple(() =>
        {
            Assert.That(hasToken, Is.False);
            Assert.That(token, Is.Null);
            Assert.That(session.IsAuthenticated, Is.False);
            Assert.That(session.CurrentUser, Is.Null);
            Assert.That(changes, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task AuthorizationHandler_ActiveSession_AddsBearerToken()
    {
        var clock = new TestTimeProvider(
            new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));
        var session = new AuthenticationSession(clock);
        session.Start(CreateAuthenticationResponse(clock.GetUtcNow().AddMinutes(15)));
        string? authorization = null;
        var transport = new StubHttpMessageHandler(request =>
        {
            authorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = new HttpClient(
            new SessionAuthorizationHandler(session, transport));

        await httpClient.GetAsync("https://flatplate.example/api/profile");

        Assert.That(authorization, Is.EqualTo("Bearer signed-token"));
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler) =>
        new(handler)
        {
            BaseAddress = new Uri("https://flatplate.example/api/")
        };

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string json) => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static AuthenticationResponse CreateAuthenticationResponse(
        DateTimeOffset expiresAtUtc) => new(
            "signed-token",
            expiresAtUtc,
            new AuthenticatedUser(
                Guid.NewGuid(),
                "Jonathan",
                "jonathan@example.com",
                true));

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    private sealed class TestTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}
