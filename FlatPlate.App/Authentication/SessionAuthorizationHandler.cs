using System.Net.Http;
using System.Net.Http.Headers;

namespace FlatPlate.App.Authentication;

/// <summary>
/// Adds the current bearer token to protected API requests without exposing it to view models.
/// </summary>
public sealed class SessionAuthorizationHandler(
    IAuthenticationSession session,
    HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (session.TryGetAccessToken(out var accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                accessToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
