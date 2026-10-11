using FlatPlate.Server.Models;
using Microsoft.Extensions.Logging;

namespace FlatPlate.Server.Messaging;

/// <summary>
/// Records that delivery is pending until an email provider is configured.
/// </summary>
public sealed class DeferredAccountMessageSender(
    ILogger<DeferredAccountMessageSender> logger) : IAccountMessageSender
{
    public Task SendEmailVerificationAsync(
        UserAccount account,
        string code,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Email verification delivery requested for account {AccountId}.",
            account.Id);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(
        UserAccount account,
        string code,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Password reset delivery requested for account {AccountId}.",
            account.Id);
        return Task.CompletedTask;
    }
}
