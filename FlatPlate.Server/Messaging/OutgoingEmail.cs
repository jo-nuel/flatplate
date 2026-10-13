namespace FlatPlate.Server.Messaging;

/// <summary>
/// Contains the non-provider-specific parts of an email message.
/// </summary>
public sealed record OutgoingEmail(
    string RecipientName,
    string RecipientAddress,
    string Subject,
    string TextBody,
    string HtmlBody);
