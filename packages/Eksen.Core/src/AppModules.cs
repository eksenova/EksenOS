namespace Eksen.Core;

public static class AppModules
{
    public static string Eksen
    {
        get { return "Eksen"; }
    }
}

public static class AppModuleRegistry
{
    private static readonly HashSet<string> InternalRegisteredModules = [];

    public static IReadOnlyCollection<string> RegisteredModules
    {
        get { return InternalRegisteredModules.AsReadOnly(); }
    }

    public static void Register(string moduleName)
    {
        InternalRegisteredModules.Add(moduleName);
    }
}