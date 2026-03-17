using Eksen.SmartEnums.OpenApi;
using Eksen.SmartEnums.OpenApi.Tests.Fakes;
using Eksen.TestBase;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Eksen.SmartEnums.OpenApi.Tests;

public class DependencyInjectionExtensionsTests : EksenUnitTestBase
{
    [Fact]
    public void AddOpenApiSupport_Should_Register_OpenApi_Options()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddEksen(eksen =>
        {
            eksen.AddSmartEnums(builder =>
            {
                builder.Configure(options => options.Add<TestColor>());
                builder.AddOpenApiSupport();
            });
        });

        var provider = services.BuildServiceProvider();

        // Assert
        var optionsMonitor = provider.GetService<IOptionsMonitor<OpenApiOptions>>();
        optionsMonitor.ShouldNotBeNull();
    }
}
