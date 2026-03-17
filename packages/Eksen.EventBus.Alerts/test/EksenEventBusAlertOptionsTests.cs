using Eksen.TestBase;
using Shouldly;

namespace Eksen.EventBus.Alerts.Tests;

public class EksenEventBusAlertOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void Defaults_Should_Have_Expected_Values()
    {
        // Arrange & Act
        var options = new EksenEventBusAlertOptions();

        // Assert
        options.IsEnabled.ShouldBeTrue();
        options.EnabledChannels.ShouldBeEmpty();
    }
}
