using Eksen.SmartEnums;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenSmartEnumsBuilder AddAspNetCoreSupport(this IEksenSmartEnumsBuilder builder)
    {
        var services = builder.Services;

        services.AddOptions<JsonOptions>()
            .PostConfigure<IOptions<EksenSmartEnumOptions>>((jsonOptions, smartEnumOptions) =>
            {
                smartEnumOptions.Value.ConfigureJsonOptions(jsonOptions.SerializerOptions);
            });

        services.AddOptions<Microsoft.AspNetCore.Mvc.JsonOptions>()
            .PostConfigure<IOptions<EksenSmartEnumOptions>>((mvcJsonOptions, smartEnumOptions) =>
            {
                smartEnumOptions.Value.ConfigureJsonOptions(mvcJsonOptions.JsonSerializerOptions);
            });

        return builder;
    }
}