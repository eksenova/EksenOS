using Eksen.ValueObjects.Emailing;

namespace Eksen.Emailing;

public sealed record SendTemplateEmailParameters<TTemplateModel>
{
    public required ICollection<EmailAddress> To { get; init; }

    public string? Subject { get; init; }

    public required string TemplateKey { get; init; }

    public required TTemplateModel Model { get; set; }

    public string? FromName { get; set; }

    public EmailAddress? FromAddress { get; set; }
}