using Eksen.Core.Text;

namespace Eksen.TestBase.Fakes;

public sealed class FakeRandomStringGenerator : IRandomStringGenerator
{
    public string GenerateRandomString(RandomStringGenerationParameters? parameters = null)
    {
        var length = parameters?.Length ?? 32;
        return new string('a', length);
    }
}
