using Cysharp.Serialization.Json;
using Eksen.Ulid.AspNetCore;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace

public static class DependencyInjectionExtensions
{
    public static IEksenUlidBuilder AddAspNetCoreSupport(this IEksenUlidBuilder builder)
    {
        var services = builder.Services;

        services.AddRouting(options =>
            options.ConstraintMap.Add(UlidRouteConstraint.UlidContraint, typeof(UlidRouteConstraint)));

        services.AddMvcCore()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(
                    new UlidJsonConverter());
            });

        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.Converters.Add(
                new UlidJsonConverter());
        });

        return builder;
    }
}