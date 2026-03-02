using Eksen.SmartEnums;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenSmartEnumsBuilder AddAspNetCoreSupport(this IEksenSmartEnumsBuilder builder)
    {
        var services = builder.Services;

        services.PostConfigure<EksenSmartEnumOptions>(smartEnumOptions =>
        {
            services.ConfigureHttpJsonOptions(jsonOptions =>
            {
                smartEnumOptions.ConfigureJsonOptions(jsonOptions.SerializerOptions);
            });

            services.AddMvcCore()
                .AddJsonOptions(jsonOptions =>
                {
                    smartEnumOptions.ConfigureJsonOptions(jsonOptions.JsonSerializerOptions);
                });
        });

        return builder;
    }
}