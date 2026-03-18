using Microsoft.Extensions.Logging;

namespace Hist.Server.Collection.Adapters.Tiingo;

/// <summary>
/// Proactive hourly request throttle for the Tiingo API.
/// Tiingo returns HTTP 200 with an empty CSV body when the hourly limit is
/// exceeded — indistinguishable from "no more data". This limiter counts
/// outgoing requests and blocks before the limit is reached.
/// </summary>
public class TiingoRateLimiter(int hourlyLimit, ILogger<TiingoRateLimiter> logger)
{
    private int _count = 0;
    private DateTimeOffset _windowStart = HourStart(DateTimeOffset.UtcNow);
    private readonly Lock _lock = new();

    private static DateTimeOffset HourStart(DateTimeOffset t) =>
        new(t.Year, t.Month, t.Day, t.Hour, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Call before every Tiingo HTTP request. Blocks until the next hour
    /// boundary if the hourly limit has been reached.
    /// </summary>
    public async Task ThrottleAsync(CancellationToken ct)
    {
        while (true)
        {
            DateTimeOffset? waitUntil = null;
            lock (_lock)
            {
                var now = DateTimeOffset.UtcNow;
                var newWindow = HourStart(now);
                if (newWindow > _windowStart)
                {
                    _windowStart = newWindow;
                    _count = 0;
                }

                if (_count < hourlyLimit)
                {
                    _count++;
                    return;
                }

                waitUntil = _windowStart.AddHours(1);
            }

            var delay = waitUntil.Value - DateTimeOffset.UtcNow;
            logger.LogWarning(
                "Tiingo hourly request limit ({Limit}) reached — suspending until {Until:HH:mm} UTC",
                hourlyLimit, waitUntil.Value);

            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);
        }
    }
}
