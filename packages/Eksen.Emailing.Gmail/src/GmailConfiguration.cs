using System.ComponentModel.DataAnnotations;
using Eksen.ValueObjects.Emailing;

namespace Eksen.Emailing.Gmail;

public sealed record GmailConfiguration
{
    public const string DefaultConfigSectionPath = "GoogleCloud:Gmail";

    public const string DefaultServiceAccountFile = "service-account.json";

    public const string DefaultApplicationName = "Eksen";

    [Required]
    public required EmailAddress DefaultFromAddress { get; set; }

    [Required]
    public required string DefaultFromName { get; set; }

    /// <summary>
    /// Path to the Google service-account JSON key file used for domain-wide delegation.
    /// </summary>
    [Required]
    public string ServiceAccountFile { get; set; } = DefaultServiceAccountFile;

    /// <summary>
    /// The mailbox the service account impersonates via domain-wide delegation.
    /// When not set, <see cref="DefaultFromAddress"/> is used as the impersonated user.
    /// </summary>
    public EmailAddress? ImpersonatedUser { get; set; }

    /// <summary>
    /// Application name reported to the Gmail API.
    /// </summary>
    public string ApplicationName { get; set; } = DefaultApplicationName;
}
