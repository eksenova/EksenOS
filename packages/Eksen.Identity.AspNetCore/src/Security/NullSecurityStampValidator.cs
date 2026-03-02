using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Eksen.Identity.AspNetCore.Security;

internal sealed class NullSecurityStampValidator : ISecurityStampValidator
{
    public Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        return Task.CompletedTask;
    }
}