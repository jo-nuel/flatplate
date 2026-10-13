using FlatPlate.Server.Messaging;
using FlatPlate.Server.Models;

namespace FlatPlate.Tests;

/// <summary>
/// Checks the account email content before it reaches an external SMTP provider.
/// </summary>
public sealed class AccountEmailMessageSenderTests
{
    [Test]
    public async Task SendEmailVerificationAsync_CreatesAddressedExpiringCodeMessage()
    {
        var transport = new CapturingEmailTransport();
        var sender = new AccountEmailMessageSender(transport);
        var account = new UserAccount
        {
            DisplayName = "Jonathan & Team",
            Email = "jonathan@example.com"
        };

        await sender.SendEmailVerificationAsync(account, "123456");

        Assert.Multiple(() =>
        {
            Assert.That(transport.Email!.RecipientAddress, Is.EqualTo(account.Email));
            Assert.That(transport.Email.Subject, Does.Contain("Verify"));
            Assert.That(transport.Email.TextBody, Does.Contain("123456"));
            Assert.That(transport.Email.TextBody, Does.Contain("30 minutes"));
            Assert.That(transport.Email.HtmlBody, Does.Contain("Jonathan &amp; Team"));
        });
    }

    [Test]
    public async Task SendPasswordResetAsync_IncludesSafetyGuidance()
    {
        var transport = new CapturingEmailTransport();
        var sender = new AccountEmailMessageSender(transport);
        var account = new UserAccount
        {
            DisplayName = "Jonathan",
            Email = "jonathan@example.com"
        };

        await sender.SendPasswordResetAsync(account, "654321");

        Assert.Multiple(() =>
        {
            Assert.That(transport.Email!.Subject, Does.Contain("Reset"));
            Assert.That(transport.Email.TextBody, Does.Contain("654321"));
            Assert.That(transport.Email.TextBody, Does.Contain("15 minutes"));
            Assert.That(transport.Email.TextBody, Does.Contain("ignore this email"));
        });
    }

    [Test]
    public void SmtpSettings_UsernameWithoutPassword_IsRejected()
    {
        var settings = new SmtpSettings(
            "smtp.example.com",
            587,
            SmtpSecurityMode.StartTls,
            "noreply@example.com",
            "FlatPlate",
            "smtp-user",
            null);

        Assert.That(settings.Validate, Throws.ArgumentException);
    }

    private sealed class CapturingEmailTransport : IEmailTransport
    {
        public OutgoingEmail? Email { get; private set; }

        public Task SendAsync(
            OutgoingEmail email,
            CancellationToken cancellationToken = default)
        {
            Email = email;
            return Task.CompletedTask;
        }
    }
}
