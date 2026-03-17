using Eksen.TestBase;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eksen.UnitOfWork.AspNetCore.Tests;

public class DependencyInjectionExtensionsTests : EksenUnitTestBase
{
    [Fact]
    public void UseUnitOfWork_Should_Return_ApplicationBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEksen(builder => builder.AddUnitOfWork());
        var sp = services.BuildServiceProvider();
        var app = new ApplicationBuilder(sp);

        // Act
        var result = app.UseUnitOfWork();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeAssignableTo<IApplicationBuilder>();
    }
}
