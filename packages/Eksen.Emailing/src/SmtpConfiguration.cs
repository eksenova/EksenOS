using System.ComponentModel.DataAnnotations;
using Eksen.ValueObjects.Emailing;

namespace Eksen.Emailing;

public sealed record SmtpConfiguration
{
    public const string DefaultConfigSectionPath = "Smtp";

    [Required]
    public required EmailAddress DefaultFromAddress { get; set; }

    [Required]
    public required string DefaultFromName { get; set; }

    [Required]
    public required string Host { get; set; }

    [Required]
    public required int Port { get; set; }

    [Required]
    public required string UserName { get; set; }

    [Required]
    public required string Password { get; set; }

    public bool EnableTls { get; set; } = true;
}