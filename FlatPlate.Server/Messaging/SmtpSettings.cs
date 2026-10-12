using System.Net.Mail;

namespace FlatPlate.Server.Messaging;

/// <summary>
/// Holds SMTP connection settings supplied by the API environment.
/// </summary>
public sealed record SmtpSettings(
    string Host,
    int Port,
    SmtpSecurityMode SecurityMode,
    string FromAddress,
    string FromName,
    string? Username,
    string? Password)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Port);

        if (Port > 65_535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Port),
                "SMTP port must be between 1 and 65535.");
        }

        if (!MailAddress.TryCreate(FromAddress, out _))
        {
            throw new ArgumentException(
                "SMTP sender address must be a valid email address.",
                nameof(FromAddress));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(FromName);

        if (string.IsNullOrWhiteSpace(Username) != string.IsNullOrWhiteSpace(Password))
        {
            throw new ArgumentException(
                "SMTP username and password must either both be configured or both be omitted.");
        }
    }
}
