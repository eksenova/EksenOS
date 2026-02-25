#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddEksen(
        this IServiceCollection services,
        Action<IEksenBuilder> configureAction
    )
    {
        var builder = new EksenBuilder(services);
        configureAction(builder);

        return services;
    }
}

public interface IEksenBuilder
{
    IServiceCollection Services { get; }
}

public class EksenBuilder(IServiceCollection services) : IEksenBuilder
{
    public IServiceCollection Services { get; } = services;
}
