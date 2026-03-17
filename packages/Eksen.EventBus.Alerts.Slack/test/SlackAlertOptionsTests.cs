using Eksen.TestBase;
using Shouldly;

namespace Eksen.EventBus.Alerts.Slack.Tests;

public class SlackAlertOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void Defaults_Should_Have_Expected_Values()
    {
        // Arrange & Act
        var options = new SlackAlertOptions();

        // Assert
        options.WebhookUrl.ShouldBeNull();
        options.Channel.ShouldBeNull();
        options.Username.ShouldBe("Eksen EventBus");
        options.IconEmoji.ShouldBe(":rotating_light:");
    }
}
