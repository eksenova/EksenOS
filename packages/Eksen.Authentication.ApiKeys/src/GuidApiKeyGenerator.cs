namespace Eksen.Authentication.ApiKeys;

public class GuidApiKeyGenerator : IApiKeyGenerator
{
    public virtual string Generate()
    {
        return Guid.NewGuid().ToString("N");
    }
}
