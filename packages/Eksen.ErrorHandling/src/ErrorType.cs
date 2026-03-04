namespace Eksen.ErrorHandling;

public static class ErrorType
{
    public static string NotFound
    {
        get { return nameof(NotFound); }
    }

    public static string Authorization
    {
        get { return nameof(Authorization); }
    }

    public static string Validation
    {
        get { return nameof(Validation); }
    }

    public static string Conflict
    {
        get { return nameof(Conflict); }
    }

    public static string RateLimit
    {
        get { return nameof(RateLimit); }
    }
}