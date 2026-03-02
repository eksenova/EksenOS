using Eksen.ValueObjects;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenValueObjectsBuilder AddAspNetCoreSupport(this IEksenValueObjectsBuilder builder)
    {
        var services = builder.Services;

        services.PostConfigure<EksenValueObjectOptions>(valueObjectOptions =>
        {
            services.ConfigureHttpJsonOptions(jsonOptions =>
            {
                valueObjectOptions.ConfigureJsonOptions(jsonOptions.SerializerOptions);
            });

            services.AddMvcCore()
                .AddJsonOptions(jsonOptions =>
                {
                    valueObjectOptions.ConfigureJsonOptions(jsonOptions.JsonSerializerOptions);
                });
        });

        return builder;
    }
}