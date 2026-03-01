using System.Reflection;

namespace Eksen.DataSeeding;

public class EksenDataSeedingOptions
{
    public HashSet<Type> SeedContributors { get; set; } = [];

    public void Add(Type type)
    {
        SeedContributors.Add(type);
    }

    public void AddRange(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            Add(type);
        }
    }

    public void AddAssembly(Assembly assembly)
    {
        List<Type> seedContributorTypes;

        try
        {
            seedContributorTypes = assembly.GetTypes().ToList();
        }
        catch (ReflectionTypeLoadException typeLoadException)
        {
            seedContributorTypes = typeLoadException.Types
                .Where(x => x != null)
                .Select(x => x!)
                .ToList();
        }

        seedContributorTypes = seedContributorTypes.Where(x => x.IsClass)
            .Where(typeof(IDataSeedContributor).IsAssignableFrom)
            .Where(x => !x.IsAbstract)
            .ToList();

        AddRange(seedContributorTypes);
    }
}