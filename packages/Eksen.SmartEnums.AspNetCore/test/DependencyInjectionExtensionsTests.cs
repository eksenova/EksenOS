using Eksen.SmartEnums;
using Eksen.SmartEnums.AspNetCore.Tests.Fakes;
using Eksen.TestBase;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Eksen.SmartEnums.AspNetCore.Tests;

public class DependencyInjectionExtensionsTests : EksenUnitTestBase
{
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddEksen(eksen =>
        {
            eksen.AddSmartEnums(builder =>
            {
                builder.Configure(options => options.Add<TestColor>());
                builder.AddAspNetCoreSupport();
            });
        });

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddAspNetCoreSupport_Should_Configure_MinimalApi_JsonOptions()
    {
        // Arrange
        using var provider = BuildServiceProvider();

        // Act
        var jsonOptions = provider.GetRequiredService<IOptions<JsonOptions>>().Value;

        // Assert
        jsonOptions.SerializerOptions.Converters
            .ShouldContain(c => c is JsonStringSmartEnumConverter<TestColor>);
    }

    [Fact]
    public void AddAspNetCoreSupport_Should_Configure_Mvc_JsonOptions()
    {
        // Arrange
        using var provider = BuildServiceProvider();

        // Act
        var mvcJsonOptions = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value;

        // Assert
        mvcJsonOptions.JsonSerializerOptions.Converters
            .ShouldContain(c => c is JsonStringSmartEnumConverter<TestColor>);
    }

    [Fact]
    public void AddAspNetCoreSupport_Should_Register_SmartEnumOptions()
    {
        // Arrange
        using var provider = BuildServiceProvider();

        // Act
        var smartEnumOptions = provider.GetRequiredService<IOptions<EksenSmartEnumOptions>>().Value;

        // Assert
        smartEnumOptions.EnumerationTypes.ShouldContain(typeof(TestColor));
    }
}
