using Eksen.EventBus.Retry;
using Eksen.TestBase;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Eksen.EventBus.Tests;

public class EventRetryPipelineProviderTests : EksenUnitTestBase
{
    [Fact]
    public void GetPipeline_Should_Return_NonNull_Pipeline()
    {
        // Arrange
        var options = new EksenEventBusOptions
        {
            Retry = new RetryOptions
            {
                MaxRetryAttempts = 3,
                InitialDelay = TimeSpan.FromMilliseconds(10),
                MaxDelay = TimeSpan.FromMilliseconds(100),
                BackoffMultiplier = 2.0
            }
        };
        var provider = new EventRetryPipelineProvider(Options.Create(options));

        // Act
        var pipeline = provider.GetPipeline();

        // Assert
        pipeline.ShouldNotBeNull();
    }

    [Fact]
    public void GetPipeline_Should_Return_Same_Instance()
    {
        // Arrange
        var provider = new EventRetryPipelineProvider(Options.Create(new EksenEventBusOptions()));

        // Act
        var pipeline1 = provider.GetPipeline();
        var pipeline2 = provider.GetPipeline();

        // Assert
        ReferenceEquals(pipeline1, pipeline2).ShouldBeTrue();
    }
}
