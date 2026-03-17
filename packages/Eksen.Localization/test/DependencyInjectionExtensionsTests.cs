using Eksen.Core;
using Eksen.Localization.Formatting;
using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eksen.Localization.Tests;

public class DependencyInjectionExtensionsTests : EksenUnitTestBase
{
    [Fact]
    public void AddLocalization_Should_Register_MessageFormatter()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEksen(builder =>
        {
            builder.AddLocalization();
        });

        var sp = services.BuildServiceProvider();

        // Assert
        var formatter = sp.GetService<IMessageFormatter>();
        formatter.ShouldNotBeNull();
        formatter.ShouldBeOfType<SmartFormatMessageFormatter>();
    }

    [Fact]
    public void AddLocalization_Should_Register_As_Singleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEksen(builder =>
        {
            builder.AddLocalization();
        });

        // Assert
        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IMessageFormatter));
        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddLocalization_Should_Register_Module()
    {
        // Arrange & Act
        // Accessing AppModules.Localization triggers static constructor registration
        var moduleName = AppModules.Localization;

        // Assert
        moduleName.ShouldBe("Eksen.Localization");
        AppModuleRegistry.RegisteredModules.ShouldContain("Eksen.Localization");
    }
}
