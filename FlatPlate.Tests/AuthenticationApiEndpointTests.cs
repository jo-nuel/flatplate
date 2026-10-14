using FlatPlate.App.Authentication;

namespace FlatPlate.Tests;

/// <summary>
/// Checks deployed and local authentication API endpoint selection.
/// </summary>
public sealed class AuthenticationApiEndpointTests
{
    [Test]
    public void Resolve_NoOverride_UsesDeployedAzureApi()
    {
        var endpoint = AuthenticationApiEndpoint.Resolve(null);

        Assert.That(
            endpoint.AbsoluteUri,
            Is.EqualTo(
                "https://func-flatplate-jonathan-1405-gjgngbgvfzdvfxh7." +
                "australiaeast-01.azurewebsites.net/api/"));
    }

    [Test]
    public void Resolve_LocalOverrideWithoutTrailingSlash_NormalizesAddress()
    {
        var endpoint = AuthenticationApiEndpoint.Resolve("http://localhost:7071/api");

        Assert.That(endpoint.AbsoluteUri, Is.EqualTo("http://localhost:7071/api/"));
    }

    [Test]
    public void Resolve_RemoteHttpOverride_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => AuthenticationApiEndpoint.Resolve("http://example.com/api/"));

        Assert.That(exception!.Message, Does.Contain("HTTPS"));
    }

    [Test]
    public void Resolve_AddressWithoutApiPath_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => AuthenticationApiEndpoint.Resolve("https://example.com/"));

        Assert.That(exception!.Message, Does.Contain("/api/"));
    }

    [Test]
    public void Resolve_InvalidOverride_Throws()
    {
        Assert.That(
            () => AuthenticationApiEndpoint.Resolve("not a URL"),
            Throws.TypeOf<InvalidOperationException>());
    }
}
