using System.Net;
using FlatPlate.Server.Models;

namespace FlatPlate.Server.Messaging;

/// <summary>
/// Creates clear account emails and delegates provider-specific delivery.
/// </summary>
public sealed class AccountEmailMessageSender : IAccountMessageSender
{
    private readonly IEmailTransport _transport;
    private readonly Uri _verificationEndpoint;

    public AccountEmailMessageSender(
        IEmailTransport transport,
        Uri publicApiBaseAddress)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        ArgumentNullException.ThrowIfNull(publicApiBaseAddress);

        if (!publicApiBaseAddress.IsAbsoluteUri
            || (publicApiBaseAddress.Scheme != Uri.UriSchemeHttps
                && !(publicApiBaseAddress.Scheme == Uri.UriSchemeHttp
                    && publicApiBaseAddress.IsLoopback)))
        {
            throw new ArgumentException(
                "The public API address must use HTTPS, except for local development.",
                nameof(publicApiBaseAddress));
        }

        var normalizedBaseAddress = new Uri(
            $"{publicApiBaseAddress.AbsoluteUri.TrimEnd('/')}/");
        _verificationEndpoint = new Uri(
            normalizedBaseAddress,
            "auth/verify-email-link");
    }

    public Task SendEmailVerificationAsync(
        UserAccount account,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var verificationLink = BuildVerificationLink(account.Email, code);

        return _transport.SendAsync(new OutgoingEmail(
            account.DisplayName,
            account.Email,
            "Verify your FlatPlate email",
            $"Your FlatPlate verification code is {code}. " +
            $"It expires in 30 minutes. Verify your email: {verificationLink}",
            BuildHtml(
                account.DisplayName,
                "Verify your email",
                code,
                "This code expires in 30 minutes.",
                verificationLink)),
            cancellationToken);
    }

    public Task SendPasswordResetAsync(
        UserAccount account,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return _transport.SendAsync(new OutgoingEmail(
            account.DisplayName,
            account.Email,
            "Reset your FlatPlate password",
            $"Your FlatPlate password reset code is {code}. " +
            "It expires in 15 minutes. If you did not request this, ignore this email.",
            BuildHtml(
                account.DisplayName,
                "Reset your password",
                code,
                "This code expires in 15 minutes. If you did not request it, " +
                "you can safely ignore this email.")),
            cancellationToken);
    }

    private static string BuildHtml(
        string displayName,
        string heading,
        string code,
        string footer,
        Uri? actionLink = null)
    {
        var safeName = WebUtility.HtmlEncode(displayName);
        var safeHeading = WebUtility.HtmlEncode(heading);
        var safeCode = WebUtility.HtmlEncode(code);
        var safeFooter = WebUtility.HtmlEncode(footer);
        var action = actionLink is null
            ? string.Empty
            : $$"""
                <p style="margin:24px 0">
                  <a href="{{WebUtility.HtmlEncode(actionLink.AbsoluteUri)}}"
                     style="background:#2F7D57;color:#fff;padding:12px 20px;text-decoration:none;border-radius:6px">
                    Verify email
                  </a>
                </p>
                """;

        return $$"""
            <!doctype html>
            <html lang="en">
              <body style="font-family:Segoe UI,Arial,sans-serif;color:#173D2B">
                <h2>{{safeHeading}}</h2>
                <p>Hello {{safeName}},</p>
                <p>Enter this code in FlatPlate:</p>
                <p style="font-size:28px;font-weight:700;letter-spacing:6px">{{safeCode}}</p>
                {{action}}
                <p>{{safeFooter}}</p>
              </body>
            </html>
            """;
    }

    private Uri BuildVerificationLink(string email, string code)
    {
        var builder = new UriBuilder(_verificationEndpoint)
        {
            Query = $"email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(code)}"
        };

        return builder.Uri;
    }
}
