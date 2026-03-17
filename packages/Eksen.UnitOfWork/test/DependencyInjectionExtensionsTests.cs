using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eksen.UnitOfWork.Tests;

public class DependencyInjectionExtensionsTests : EksenUnitTestBase
{
    [Fact]
    public void AddUnitOfWork_Should_Register_UnitOfWorkManager()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEksen(builder =>
        {
            builder.AddUnitOfWork();
        });

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        // Assert
        var manager = scope.ServiceProvider.GetService<IUnitOfWorkManager>();
        manager.ShouldNotBeNull();
        manager.ShouldBeOfType<UnitOfWorkManager>();
    }

    [Fact]
    public void AddUnitOfWork_Should_Invoke_Configure_Action()
    {
        // Arrange
        var services = new ServiceCollection();
        var configureActionCalled = false;

        // Act
        services.AddEksen(builder =>
        {
            builder.AddUnitOfWork(uowBuilder =>
            {
                configureActionCalled = true;
                uowBuilder.EksenBuilder.ShouldNotBeNull();
                uowBuilder.Services.ShouldNotBeNull();
            });
        });

        // Assert
        configureActionCalled.ShouldBeTrue();
    }

    [Fact]
    public void AddUnitOfWork_Should_Register_Scoped_Lifetime()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEksen(builder =>
        {
            builder.AddUnitOfWork();
        });

        // Assert
        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IUnitOfWorkManager));
        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }
}
