using Eksen.ValueObjects.Emailing;

namespace Eksen.Emailing;

public record EmailViewModel<TDataModel>
{
    public required EmailAddress To { get; init; }

    public required TDataModel Data { get; init; }

    public string? FromName { get; set; }

    public EmailAddress? FromAddress { get; set; }

    public string? Subject { get; set; }
}