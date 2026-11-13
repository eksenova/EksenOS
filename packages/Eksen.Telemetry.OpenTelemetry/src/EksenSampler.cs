using System.Diagnostics;
using OpenTelemetry.Trace;

namespace Eksen.Telemetry.OpenTelemetry;

public static class EksenSampler
{
    /// <summary>
    /// Builds the trace sampler. Roots and remote parents (sampled or not) are all decided by
    /// <see cref="TraceIdRatioBasedSampler"/> on the trace id, so an incoming <c>traceparent</c> cannot force
    /// sampling and services with the same ratio agree; local children follow their parent.
    /// Unless <paramref name="traceOrphanClientSpans"/> is set, a client span that starts with no parent at all (a
    /// database, cache or HTTP call made outside any request, message or job span, such as a background poll's query)
    /// is dropped instead of becoming a one-span trace of its own.
    /// </summary>
    public static Sampler Create(double ratio, bool traceOrphanClientSpans = false)
    {
        if (ratio is < 0 or > 1 || double.IsNaN(ratio))
        {
            throw new ArgumentOutOfRangeException(nameof(ratio), ratio, "The sampling ratio must be between 0 and 1.");
        }

        Sampler root = new TraceIdRatioBasedSampler(ratio);
        if (!traceOrphanClientSpans)
        {
            root = new OrphanClientSpanSampler(root);
        }

        return new ParentBasedSampler(
            root,
            remoteParentSampled: new TraceIdRatioBasedSampler(ratio),
            remoteParentNotSampled: new TraceIdRatioBasedSampler(ratio),
            localParentSampled: new AlwaysOnSampler(),
            localParentNotSampled: new AlwaysOffSampler());
    }

    /// <summary>
    /// The root sampler: drops root spans of kind <see cref="ActivityKind.Client"/> and lets <paramref name="inner"/>
    /// decide every other root.
    /// </summary>
    private sealed class OrphanClientSpanSampler(Sampler inner) : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            return samplingParameters.Kind == ActivityKind.Client
                ? new SamplingResult(SamplingDecision.Drop)
                : inner.ShouldSample(samplingParameters);
        }
    }
}
