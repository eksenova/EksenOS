using Google.Apis.Gmail.v1.Data;
using MimeKit;
using MimeKit.Text;

namespace Eksen.Emailing.Gmail;

/// <summary>
/// Builds Gmail API <see cref="Message"/> instances from MIME content.
/// Isolated from the Gmail transport so message construction can be unit tested offline.
/// </summary>
internal static class GmailMessageFactory
{
    public static MimeMessage CreateMimeMessage(
        string fromAddress,
        string fromName,
        string toAddress,
        string subject,
        string body,
        EmailContentType contentType)
    {
        var mime = new MimeMessage();

        var fromMailbox = MailboxAddress.Parse(fromAddress);
        fromMailbox.Name = fromName;
        mime.From.Add(fromMailbox);

        mime.To.Add(MailboxAddress.Parse(toAddress));
        mime.Subject = subject;

        var textFormat = contentType == EmailContentType.Plaintext
            ? TextFormat.Plain
            : TextFormat.Html;

        mime.Body = new TextPart(textFormat)
        {
            Text = body
        };

        return mime;
    }

    public static Message CreateRawMessage(
        string fromAddress,
        string fromName,
        string toAddress,
        string subject,
        string body,
        EmailContentType contentType)
    {
        var mime = CreateMimeMessage(
            fromAddress,
            fromName,
            toAddress,
            subject,
            body,
            contentType);

        using var ms = new MemoryStream();
        mime.WriteTo(ms);

        return new Message
        {
            Raw = Base64UrlEncode(ms.ToArray())
        };
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace(oldValue: "+", newValue: "-")
            .Replace(oldValue: "/", newValue: "_")
            .Replace(oldValue: "=", newValue: "");
    }
}
