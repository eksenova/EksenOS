#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddEksenIdentity(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddHttpContextAccessor();

        return serviceCollection;
    }
}