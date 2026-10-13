using System.Net;
using FlatPlate.Server.Models;

namespace FlatPlate.Server.Messaging;

/// <summary>
/// Creates clear account emails and delegates provider-specific delivery.
/// </summary>
public sealed class AccountEmailMessageSender(IEmailTransport transport)
    : IAccountMessageSender
{
    public Task SendEmailVerificationAsync(
        UserAccount account,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return transport.SendAsync(new OutgoingEmail(
            account.DisplayName,
            account.Email,
            "Verify your FlatPlate email",
            $"Your FlatPlate verification code is {code}. " +
            "It expires in 30 minutes.",
            BuildHtml(
                account.DisplayName,
                "Verify your email",
                code,
                "This code expires in 30 minutes.")),
            cancellationToken);
    }

    public Task SendPasswordResetAsync(
        UserAccount account,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return transport.SendAsync(new OutgoingEmail(
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
        string footer)
    {
        var safeName = WebUtility.HtmlEncode(displayName);
        var safeHeading = WebUtility.HtmlEncode(heading);
        var safeCode = WebUtility.HtmlEncode(code);
        var safeFooter = WebUtility.HtmlEncode(footer);

        return $$"""
            <!doctype html>
            <html lang="en">
              <body style="font-family:Segoe UI,Arial,sans-serif;color:#173D2B">
                <h2>{{safeHeading}}</h2>
                <p>Hello {{safeName}},</p>
                <p>Enter this code in FlatPlate:</p>
                <p style="font-size:28px;font-weight:700;letter-spacing:6px">{{safeCode}}</p>
                <p>{{safeFooter}}</p>
              </body>
            </html>
            """;
    }
}
