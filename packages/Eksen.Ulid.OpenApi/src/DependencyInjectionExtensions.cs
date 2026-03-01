using Eksen.Ulid.OpenApi;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenUlidBuilder AddOpenApiSupport(this IEksenUlidBuilder builder)
    {
        var services = builder.Services;

        services.AddOpenApi(options =>
        {
            options.AddSchemaTransformer<UlidSchemaTransformer>();
            options.AddOperationTransformer<UlidOperationTransformer>();
        });

        return builder;
    }
}