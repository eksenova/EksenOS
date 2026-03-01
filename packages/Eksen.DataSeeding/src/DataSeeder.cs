using System.Reflection;
using Eksen.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// ReSharper disable SuspiciousTypeConversion.Global

namespace Eksen.DataSeeding;

public sealed class DataSeeder(
    IServiceProvider serviceProvider,
    IUnitOfWorkManager unitOfWork,
    IOptions<EksenDataSeedingOptions> options
) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = unitOfWork.BeginScope(isTransactional: true, cancellationToken: cancellationToken);

        var registeredContributors = options.Value.SeedContributors;

        var dataSeedContributors = new List<IDataSeedContributor>();
        AddDataSeedContributorsRecursive(dataSeedContributors, registeredContributors);

        foreach (var dataSeedContributor in dataSeedContributors)
        {
            await dataSeedContributor.SeedAsync(cancellationToken);

            switch (dataSeedContributor)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }

            await transaction.SaveChangesAsync(cancellationToken);
        }
    }

    private void AddDataSeedContributorsRecursive(
        ICollection<IDataSeedContributor> dataSeedContributors,
        ICollection<Type> registeredContributors)
    {
        foreach (var dataSeedContributor in registeredContributors)
        {
            AddDataSeedContributorsRecursive(dataSeedContributor, dataSeedContributors, registeredContributors);
        }
    }

    private void AddDataSeedContributorsRecursive(
        Type dataSeedContributorType,
        ICollection<IDataSeedContributor> dataSeedContributors,
        ICollection<Type> registeredContributors)
    {
        if (dataSeedContributors.Any(x => x.GetType() == dataSeedContributorType))
        {
            return;
        }

        var seedAfterAttribute = dataSeedContributorType.GetCustomAttribute<SeedAfterAttribute>();
        var instance = (IDataSeedContributor)ActivatorUtilities.CreateInstance(serviceProvider, dataSeedContributorType);

        if (seedAfterAttribute == null)
        {
            dataSeedContributors.Add(instance);
            return;
        }

        var seedAfterDataSeedContributor = registeredContributors.FirstOrDefault(x => x == seedAfterAttribute.Type);
        if (seedAfterDataSeedContributor == null)
        {
            throw new InvalidOperationException(
                $"SeedAfter attribute is defined for {dataSeedContributorType.Name} but {seedAfterAttribute.Type.Name} is not found.");
        }

        AddDataSeedContributorsRecursive(seedAfterDataSeedContributor, dataSeedContributors, registeredContributors);
        dataSeedContributors.Add(instance);
    }
}