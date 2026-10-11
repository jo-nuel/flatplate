using FlatPlate.Server.Models;

namespace FlatPlate.Server.Messaging;

/// <summary>
/// Sends one-time account codes without coupling authentication to an email provider.
/// </summary>
public interface IAccountMessageSender
{
    Task SendEmailVerificationAsync(
        UserAccount account,
        string code,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(
        UserAccount account,
        string code,
        CancellationToken cancellationToken = default);
}
