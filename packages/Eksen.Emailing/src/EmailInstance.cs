using Eksen.ValueObjects.Emailing;

namespace Eksen.Emailing;

public sealed record EmailInstance
{
    public required EmailAddress ToAddress { get; set; }

    public required string Content { get; set; }

    public EmailContentType? ContentType { get; set; }

    public string? Subject { get; set; }

    public string? FromName { get; set; }

    public EmailAddress? FromAddress { get; set; }
}