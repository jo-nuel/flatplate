namespace FlatPlate.Server.Messaging;

/// <summary>
/// Delivers a prepared email through the configured provider.
/// </summary>
public interface IEmailTransport
{
    Task SendAsync(
        OutgoingEmail email,
        CancellationToken cancellationToken = default);
}
