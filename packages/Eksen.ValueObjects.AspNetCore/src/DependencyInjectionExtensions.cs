using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace

public static class DependencyInjectionExtensions
{
    public static IEksenValueObjectsBuilder AddAspNetCoreSupport(this IEksenValueObjectsBuilder builder)
    {
        var services = builder.Services;
        var valueObjectOptions = builder.Options;

        services.Configure<JsonOptions>(options =>
        {
            valueObjectOptions.ConfigureJsonOptions(options.SerializerOptions);
        });

        services.AddMvcCore()
            .AddJsonOptions(options =>
            {
                valueObjectOptions.ConfigureJsonOptions(options.JsonSerializerOptions);
            });

        return builder;
    }
}