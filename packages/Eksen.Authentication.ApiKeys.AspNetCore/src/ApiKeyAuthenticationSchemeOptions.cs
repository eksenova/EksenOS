using Eksen.Authentication.ApiKeys;
using Microsoft.AspNetCore.Authentication;

namespace Eksen.Authentication.ApiKeys.AspNetCore;

public class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public EksenApiKeyAuthenticationMethod AuthenticationMethod { get; set; } = null!;
}
