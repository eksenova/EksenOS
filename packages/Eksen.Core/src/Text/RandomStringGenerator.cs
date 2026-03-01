using System.Text;

namespace Eksen.Core.Text;

public class RandomStringGenerator : IRandomStringGenerator
{
    public virtual string GenerateRandomString(RandomStringGenerationParameters? parameters = null)
    {
        parameters ??= new RandomStringGenerationParameters();

        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";

        var characterPool = new StringBuilder();
        if (parameters.IncludeUppercase)
        {
            characterPool.Append(uppercase);
        }

        if (parameters.IncludeLowercase)
        {
            characterPool.Append(lowercase);
        }

        if (parameters.IncludeDigits)
        {
            characterPool.Append(digits);
        }

        if (parameters.IncludeSpecialCharacters)
        {
            characterPool.Append(parameters.SpecialCharacters);
        }

        if (characterPool.Length == 0)
        {
            throw new ArgumentException(message: "At least one character type must be included.", nameof(parameters));
        }

        var passwordChars = new char[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            passwordChars[i] = characterPool[Random.Shared.Next(characterPool.Length)];
        }

        return new string(passwordChars);
    }
}