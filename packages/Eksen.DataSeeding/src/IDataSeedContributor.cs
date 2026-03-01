namespace Eksen.DataSeeding;

public interface IDataSeedContributor
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}