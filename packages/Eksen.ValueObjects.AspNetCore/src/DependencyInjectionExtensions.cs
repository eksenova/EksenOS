using Eksen.ValueObjects;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenValueObjectsBuilder AddAspNetCoreSupport(this IEksenValueObjectsBuilder builder)
    {
        var services = builder.Services;

        services.AddOptions<JsonOptions>()
            .PostConfigure<IOptions<EksenValueObjectOptions>>((jsonOptions, valueObjectOptions) =>
            {
                valueObjectOptions.Value.ConfigureJsonOptions(jsonOptions.SerializerOptions);
            });

        services.AddOptions<Microsoft.AspNetCore.Mvc.JsonOptions>()
            .PostConfigure<IOptions<EksenValueObjectOptions>>((mvcJsonOptions, valueObjectOptions) =>
            {
                valueObjectOptions.Value.ConfigureJsonOptions(mvcJsonOptions.JsonSerializerOptions);
            });

        return builder;
    }
}