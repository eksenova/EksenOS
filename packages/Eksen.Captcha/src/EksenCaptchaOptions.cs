namespace Eksen.Captcha;

public sealed record EksenCaptchaOptions
{
    public const string DefaultConfigSectionPath = "Eksen:Captcha";

    public bool Enabled { get; set; } = true;

    public IList<string> BypassTokens { get; set; } = [];
}
