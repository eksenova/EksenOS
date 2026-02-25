using System.Security.Claims;

namespace Eksen.Identity.Claims;

public static class ClaimExtensions
{
    public static string? GetClaim(this ClaimsPrincipal principal, string type)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentException.ThrowIfNullOrEmpty(type);

        return principal.FindAll(type).LastOrDefault()?.Value;
    }

    extension(ClaimsIdentity claimsIdentity)
    {
        public string? GetClaim(string type)
        {
            ArgumentNullException.ThrowIfNull(claimsIdentity);
            ArgumentException.ThrowIfNullOrEmpty(type);

            return claimsIdentity.FindAll(type).LastOrDefault()?.Value;
        }

        public ClaimsIdentity AddIfNotExists(Claim claim)
        {
            ArgumentNullException.ThrowIfNull(claim);

            if (claimsIdentity.FindFirst(claim.Type) == null)
            {
                claimsIdentity.AddClaim(claim);
            }

            return claimsIdentity;
        }

        public ClaimsIdentity AddOrReplace(Claim claim)
        {
            ArgumentNullException.ThrowIfNull(claim);

            foreach (var other in claimsIdentity.FindAll(claim.Type).ToList())
            {
                claimsIdentity.RemoveClaim(other);
            }

            claimsIdentity.AddClaim(claim);
            return claimsIdentity;
        }
    }
}