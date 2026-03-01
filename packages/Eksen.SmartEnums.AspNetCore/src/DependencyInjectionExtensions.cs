using Microsoft.AspNetCore.Http.Json;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenSmartEnumsBuilder AddAspNetCoreSupport(this IEksenSmartEnumsBuilder builder)
    {
        var services = builder.Services;
        var smartEnumOptions = builder.Options;

        services.Configure<JsonOptions>(options =>
        {
            smartEnumOptions.ConfigureJsonOptions(options.SerializerOptions);
        });

        services.AddMvcCore()
            .AddJsonOptions(options =>
            {
                smartEnumOptions.ConfigureJsonOptions(options.JsonSerializerOptions);
            });

        return builder;
    }
}