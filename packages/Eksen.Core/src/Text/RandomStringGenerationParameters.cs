namespace Eksen.Core.Text;

public record RandomStringGenerationParameters
{
    public byte Length { get; set; } = 8;

    public bool IncludeUppercase { get; set; } = true;

    public bool IncludeLowercase { get; set; } = true;

    public bool IncludeDigits { get; set; } = true;

    public bool IncludeSpecialCharacters { get; set; } = true;

    public string SpecialCharacters { get; set; } = "!@#*.+";
}