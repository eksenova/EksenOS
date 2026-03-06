using Eksen.SmartEnums.OpenApi;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenSmartEnumsBuilder AddOpenApiSupport(this IEksenSmartEnumsBuilder builder)
    {
        var services = builder.Services;

        services.AddOpenApi(options =>
        {
            options.AddSchemaTransformer<SmartEnumSchemaTransformer>();
            options.AddOperationTransformer<SmartEnumOperationTransformer>();
        });

        return builder;
    }
}