using System.Text.Json.Serialization.Metadata;
using Eksen.TestBase;
using Eksen.ValueObjects;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Eksen.ValueObjects.AspNetCore.Tests;

public class DependencyInjectionExtensionsTests : EksenUnitTestBase
{
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddEksen(eksen =>
        {
            eksen.AddValueObjects(builder =>
            {
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
            .ShouldContain(c => c.GetType().Name.Contains("JsonValueObject"));
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
            .ShouldContain(c => c.GetType().Name.Contains("JsonValueObject"));
    }

    [Fact]
    public void AddAspNetCoreSupport_Should_Register_ValueObjectOptions()
    {
        // Arrange
        using var provider = BuildServiceProvider();

        // Act
        var valueObjectOptions = provider.GetRequiredService<IOptions<EksenValueObjectOptions>>().Value;

        // Assert
        valueObjectOptions.ValueObjectTypes.ShouldNotBeEmpty();
    }

    [Fact]
    public void AddAspNetCoreSupport_Should_Set_TypeInfoResolver_In_MinimalApi_JsonOptions()
    {
        // Arrange
        using var provider = BuildServiceProvider();

        // Act
        var jsonOptions = provider.GetRequiredService<IOptions<JsonOptions>>().Value;

        // Assert
        jsonOptions.SerializerOptions.TypeInfoResolver.ShouldNotBeNull();
        jsonOptions.SerializerOptions.TypeInfoResolver.ShouldNotBeOfType<DefaultJsonTypeInfoResolver>();
    }

    [Fact]
    public void AddAspNetCoreSupport_Should_Set_TypeInfoResolver_In_Mvc_JsonOptions()
    {
        // Arrange
        using var provider = BuildServiceProvider();

        // Act
        var mvcJsonOptions = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value;

        // Assert
        mvcJsonOptions.JsonSerializerOptions.TypeInfoResolver.ShouldNotBeNull();
        mvcJsonOptions.JsonSerializerOptions.TypeInfoResolver.ShouldNotBeOfType<DefaultJsonTypeInfoResolver>();
    }
}
