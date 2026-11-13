using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eksen.EventBus.Telemetry;
using Eksen.Telemetry;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Eksen.EventBus.Retry;

public interface IEventRetryPipelineProvider
{
    ResiliencePipeline GetPipeline();
}

public class EventRetryPipelineProvider(
    IOptions<EksenEventBusOptions> options,
    IMeterFactory? meterFactory) : IEventRetryPipelineProvider
{
    private readonly Lazy<ResiliencePipeline> _pipeline = new(() =>
        BuildPipeline(options.Value.Retry, EventBusTelemetry.For(meterFactory)));

    public EventRetryPipelineProvider(IOptions<EksenEventBusOptions> options)
        : this(options, meterFactory: null)
    {
    }

    public ResiliencePipeline GetPipeline()
    {
        return _pipeline.Value;
    }

    private static ResiliencePipeline BuildPipeline(RetryOptions retryOptions, EventBusTelemetry telemetry)
    {
        if (retryOptions.MaxRetryAttempts <= 0)
        {
            return ResiliencePipeline.Empty;
        }

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = retryOptions.MaxRetryAttempts,
                Delay = retryOptions.InitialDelay,
                MaxDelay = retryOptions.MaxDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = arguments => ValueTask.FromResult(IsRetryable(arguments.Outcome.Exception)),
                // Exponential backoff with the configured multiplier (Polly's own exponential type always doubles).
                DelayGenerator = arguments => ValueTask.FromResult<TimeSpan?>(ExponentialBackoff.GetDelay(
                    arguments.AttemptNumber + 1,
                    retryOptions.InitialDelay,
                    retryOptions.MaxDelay,
                    retryOptions.BackoffMultiplier)),
                OnRetry = arguments =>
                {
                    RecordRetry(telemetry, arguments);
                    return default;
                }
            })
            .Build();
    }

    internal static bool IsRetryable(Exception? exception)
    {
        return exception is not null and not OperationCanceledException and not PoisonMessageException;
    }

    private static void RecordRetry(EventBusTelemetry telemetry, OnRetryArguments<object> arguments)
    {
        var properties = arguments.Context.Properties;
        var eventType = properties.GetValue(EventBusTelemetry.EventTypeProperty, "unknown");
        var handler = properties.GetValue(EventBusTelemetry.HandlerProperty, "unknown");
        var errorType = arguments.Outcome.Exception?.GetType().FullName;

        telemetry.HandlerRetries.Add(
            1,
            new KeyValuePair<string, object?>(EksenSemanticConventions.EksenEventType, eventType),
            new KeyValuePair<string, object?>(EksenSemanticConventions.EksenHandler, handler));

        // The handler span is current while the pipeline runs.
        var eventTags = new ActivityTagsCollection
        {
            { EventBusTelemetry.RetryAttemptTag, arguments.AttemptNumber + 1 },
            { "eksen.retry.delay_ms", arguments.RetryDelay.TotalMilliseconds }
        };
        if (errorType is not null)
        {
            eventTags.Add(EksenSemanticConventions.ErrorType, errorType);
        }

        Activity.Current?.AddEvent(new ActivityEvent("retry", tags: eventTags));
    }
}
