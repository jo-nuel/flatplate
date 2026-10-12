using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FlatPlate.Server.Messaging;

/// <summary>
/// Delivers account emails through a TLS-protected SMTP connection.
/// </summary>
public sealed class SmtpEmailTransport : IEmailTransport
{
    private readonly SmtpSettings _settings;

    public SmtpEmailTransport(SmtpSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
    }

    public async Task SendAsync(
        OutgoingEmail email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            _settings.FromName,
            _settings.FromAddress));
        message.To.Add(new MailboxAddress(
            email.RecipientName,
            email.RecipientAddress));
        message.Subject = email.Subject;
        message.Body = new BodyBuilder
        {
            TextBody = email.TextBody,
            HtmlBody = email.HtmlBody
        }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = _settings.SecurityMode switch
        {
            SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
            SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => throw new InvalidOperationException("Unsupported SMTP security mode.")
        };

        await client.ConnectAsync(
            _settings.Host,
            _settings.Port,
            socketOptions,
            cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                await client.AuthenticateAsync(
                    _settings.Username,
                    _settings.Password!,
                    cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None);
            }
        }
    }
}
