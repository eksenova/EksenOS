using Eksen.Core.Text;
using Eksen.TestBase.Fakes;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class FakeServiceExtensions
{
    public static IServiceCollection AddFakeRandomStringGenerator(this IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IRandomStringGenerator));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton<IRandomStringGenerator, FakeRandomStringGenerator>();
        return services;
    }
}
