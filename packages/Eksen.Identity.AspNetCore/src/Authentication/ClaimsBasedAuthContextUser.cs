using System.Security.Claims;
using Eksen.Identity.Users;
using Eksen.ValueObjects.Emailing;
using Eksen.ValueObjects.Identification;
using Microsoft.AspNetCore.Identity;

namespace Eksen.Identity.AspNetCore.Authentication;

internal sealed record ClaimsBasedAuthContextUser(
    EksenUserId UserId,
    FullName FullName,
    EmailAddress EmailAddress) : IAuthContextUser
{
    public static ClaimsBasedAuthContextUser? FromClaimsPrincipal(
        ClaimsPrincipal principal,
        IdentityOptions identityOptions)
    {
        var userIdClaimType = identityOptions.ClaimsIdentity.UserIdClaimType;
        var fullNameClaimType = identityOptions.ClaimsIdentity.UserNameClaimType;
        var emailClaimType = identityOptions.ClaimsIdentity.EmailClaimType;

        return FromClaimsPrincipal(
            principal,
            userIdClaimType,
            fullNameClaimType,
            emailClaimType);
    }

    public static ClaimsBasedAuthContextUser? FromClaimsPrincipal(
        ClaimsPrincipal principal,
        string userIdClaimType,
        string fullNameClaimType,
        string emailAddressClaimType)
    {
        var userId = principal.FindFirstValue(userIdClaimType);
        if (userId == null || !System.Ulid.TryParse(userId, out var userUlid))
        {
            return null;
        }

        var fullName = principal.FindFirstValue(fullNameClaimType);
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        var emailAddress = principal.FindFirstValue(emailAddressClaimType);
        if (string.IsNullOrWhiteSpace(emailAddress))
        {
            return null;
        }

        return new ClaimsBasedAuthContextUser(
            new EksenUserId(userUlid),
            FullName.Create(fullName),
            EmailAddress.Parse(emailAddress)
        );
    }
}