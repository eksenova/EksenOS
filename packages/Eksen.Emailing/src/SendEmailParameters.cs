using Eksen.ValueObjects.Emailing;

namespace Eksen.Emailing;

public sealed record SendEmailParameters
{
    public required ICollection<EmailInstance> To { get; set; }

    public string? Subject { get; set; }

    public string? FromName { get; set; }

    public EmailAddress? FromAddress { get; set; }
}