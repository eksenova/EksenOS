namespace Eksen.DataSeeding;

public interface IDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}