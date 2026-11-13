namespace Eksen.EventBus.Retry;

/// <summary>
/// Exponential backoff with symmetric jitter, shared by the handler retries, the outbox publish retries and the
/// delayed broker redeliveries.
/// </summary>
public static class ExponentialBackoff
{
    public const double DefaultJitterFactor = 0.2;

    /// <summary>
    /// The un-jittered delay of attempt <paramref name="attempt"/> (1-based): <c>initial * multiplier^(attempt-1)</c>,
    /// capped at <paramref name="maxDelay"/>.
    /// </summary>
    public static TimeSpan GetNominalDelay(int attempt, TimeSpan initialDelay, TimeSpan maxDelay, double multiplier)
    {
        if (attempt < 1)
        {
            attempt = 1;
        }

        if (multiplier < 1)
        {
            multiplier = 1;
        }

        var milliseconds = initialDelay.TotalMilliseconds * Math.Pow(multiplier, attempt - 1);
        if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds > maxDelay.TotalMilliseconds)
        {
            return maxDelay;
        }

        return TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
    }

    /// <summary>
    /// The nominal delay of <paramref name="attempt"/> spread by +/- <paramref name="jitterFactor"/>, never above
    /// <paramref name="maxDelay"/>.
    /// </summary>
    public static TimeSpan GetDelay(
        int attempt,
        TimeSpan initialDelay,
        TimeSpan maxDelay,
        double multiplier,
        double jitterFactor = DefaultJitterFactor,
        Random? random = null)
    {
        var nominal = GetNominalDelay(attempt, initialDelay, maxDelay, multiplier);
        var jitter = Math.Clamp(jitterFactor, 0, 1);
        var sample = (random ?? Random.Shared).NextDouble();
        var factor = 1 + (jitter * ((2 * sample) - 1));
        var milliseconds = Math.Min(nominal.TotalMilliseconds * factor, maxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
    }
}
