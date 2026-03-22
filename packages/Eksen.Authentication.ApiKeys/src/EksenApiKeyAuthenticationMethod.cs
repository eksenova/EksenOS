namespace Eksen.Authentication.ApiKeys;

public abstract record EksenApiKeyAuthenticationMethod
{
    public abstract string Type { get; }
}

public sealed record CustomHeaderAuthenticationMethod : EksenApiKeyAuthenticationMethod
{
    public override string Type => "CustomHeader";
    public string HeaderName { get; }

    internal CustomHeaderAuthenticationMethod(string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            throw new ArgumentException("Header name cannot be null or empty.", nameof(headerName));
        }

        HeaderName = headerName;
    }
}

public sealed record AuthorizationHeaderAuthenticationMethod : EksenApiKeyAuthenticationMethod
{
    public override string Type => "AuthorizationHeader";
    public string Scheme { get; }

    internal AuthorizationHeaderAuthenticationMethod(string scheme)
    {
        if (string.IsNullOrWhiteSpace(scheme))
        {
            throw new ArgumentException("Scheme cannot be null or empty.", nameof(scheme));
        }

        Scheme = scheme;
    }
}

public static class EksenApiKeyAuthenticationMethods
{
    public static CustomHeaderAuthenticationMethod CustomHeader => new("X-API-KEY");
    public static AuthorizationHeaderAuthenticationMethod AuthorizationHeader => new("Bearer");
}

public static class EksenApiKeyAuthenticationMethodExtensions
{
    extension(CustomHeaderAuthenticationMethod method)
    {
        public CustomHeaderAuthenticationMethod WithHeaderName(string headerName)
        {
            return new CustomHeaderAuthenticationMethod(headerName);
        }
    }

    extension(AuthorizationHeaderAuthenticationMethod method)
    {
        public AuthorizationHeaderAuthenticationMethod WithScheme(string scheme)
        {
            return new AuthorizationHeaderAuthenticationMethod(scheme);
        }
    }
}
