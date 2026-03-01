namespace Eksen.Core.Text;

public interface IRandomStringGenerator
{
    string GenerateRandomString(RandomStringGenerationParameters? parameters = null);
}