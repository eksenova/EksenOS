using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Eksen.EventBus.Retry;

public interface IEventRetryPipelineProvider
{
    ResiliencePipeline GetPipeline();
}

public class EventRetryPipelineProvider(IOptions<EksenEventBusOptions> options) : IEventRetryPipelineProvider
{
    private readonly Lazy<ResiliencePipeline> _pipeline = new(() => BuildPipeline(options.Value.Retry));

    public ResiliencePipeline GetPipeline()
    {
        return _pipeline.Value;
    }

    private static ResiliencePipeline BuildPipeline(RetryOptions retryOptions)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = retryOptions.MaxRetryAttempts,
                Delay = retryOptions.InitialDelay,
                MaxDelay = retryOptions.MaxDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .Build();
    }
}
