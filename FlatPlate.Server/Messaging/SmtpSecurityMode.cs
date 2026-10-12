namespace FlatPlate.Server.Messaging;

/// <summary>
/// Selects the TLS mode required by an SMTP server.
/// </summary>
public enum SmtpSecurityMode
{
    StartTls,
    SslOnConnect
}
