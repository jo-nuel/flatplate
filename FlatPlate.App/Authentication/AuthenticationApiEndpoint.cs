namespace FlatPlate.App.Authentication;

/// <summary>
/// Resolves the authentication API endpoint used by the desktop client.
/// </summary>
public static class AuthenticationApiEndpoint
{
    public const string EnvironmentVariableName = "FLATPLATE_API_BASE_URL";

    private const string DeployedAddress =
        "https://func-flatplate-jonathan-1405-gjgngbgvfzdvfxh7." +
        "australiaeast-01.azurewebsites.net/api/";

    /// <summary>
    /// Uses the deployed coursework API unless a valid local or HTTPS override is supplied.
    /// </summary>
    public static Uri Resolve(string? configuredAddress)
    {
        var address = string.IsNullOrWhiteSpace(configuredAddress)
            ? DeployedAddress
            : configuredAddress.Trim();

        if (!address.EndsWith('/'))
        {
            address += '/';
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out var baseAddress))
        {
            throw new InvalidOperationException(
                $"{EnvironmentVariableName} must be a valid absolute URL.");
        }

        var isSecure = baseAddress.Scheme == Uri.UriSchemeHttps;
        var isLocalDevelopment = baseAddress.IsLoopback
            && baseAddress.Scheme == Uri.UriSchemeHttp;
        if (!isSecure && !isLocalDevelopment)
        {
            throw new InvalidOperationException(
                $"{EnvironmentVariableName} must use HTTPS outside local development.");
        }

        if (!baseAddress.AbsolutePath.EndsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{EnvironmentVariableName} must end with /api/.");
        }

        return baseAddress;
    }
}
