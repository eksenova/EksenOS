using Eksen.TestBase;
using Shouldly;

namespace Eksen.EventBus.Tests;

public class EksenEventBusOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void Defaults_Should_Have_Default_AppName()
    {
        // Arrange & Act
        var options = new EksenEventBusOptions();

        // Assert
        options.AppName.ShouldBe("Default");
    }

    [Fact]
    public void OutboxOptions_Should_Be_Enabled_By_Default()
    {
        // Arrange & Act
        var options = new OutboxOptions();

        // Assert
        options.IsEnabled.ShouldBeTrue();
        options.BatchSize.ShouldBe(100);
        options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void InboxOptions_Should_Be_Enabled_By_Default()
    {
        // Arrange & Act
        var options = new InboxOptions();

        // Assert
        options.IsEnabled.ShouldBeTrue();
        options.IdempotencyWindow.ShouldBe(TimeSpan.FromDays(7));
    }

    [Fact]
    public void RetryOptions_Should_Have_Defaults()
    {
        // Arrange & Act
        var options = new RetryOptions();

        // Assert
        options.MaxRetryAttempts.ShouldBe(3);
        options.InitialDelay.ShouldBe(TimeSpan.FromSeconds(1));
        options.MaxDelay.ShouldBe(TimeSpan.FromSeconds(30));
        options.BackoffMultiplier.ShouldBe(2.0);
    }

    [Fact]
    public void DeadLetterOptions_Should_Be_Enabled_By_Default()
    {
        // Arrange & Act
        var options = new DeadLetterOptions();

        // Assert
        options.IsEnabled.ShouldBeTrue();
        options.MaxRetryAttemptsBeforeDeadLetter.ShouldBe(5);
    }

    [Fact]
    public void PublishOptions_Should_Default_To_Immediate_DispatchMode()
    {
        // Arrange & Act
        var options = new PublishOptions();

        // Assert
        options.DispatchMode.ShouldBe(EventDispatchMode.Immediate);
        options.TargetApp.ShouldBeNull();
        options.CorrelationId.ShouldBeNull();
        options.Delay.ShouldBeNull();
        options.Headers.ShouldBeNull();
    }
}
