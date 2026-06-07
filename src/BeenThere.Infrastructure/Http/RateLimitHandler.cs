using Microsoft.Extensions.Options;

namespace BeenThere.Infrastructure.Http;

/// <summary>
/// Delegating handler enforcing per-instance rate limiting for outbound HTTP requests.
/// Uses a simple timestamp-based approach with semaphore synchronization.
/// </summary>
public class RateLimitHandler(IOptions<Services.NominatimOptions> options) : DelegatingHandler
{
    private readonly double _minIntervalMs = GetMinInterval(options.Value.RateLimitPerSecond);
    private DateTime _lastRequest = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Lock acquisition should not be subject to request cancellation; otherwise cancelled requests
        // can leave the semaphore in an inconsistent state, causing ObjectDisposedException in subsequent requests.
        // Use CancellationToken.None explicitly to suppress CA2016 and document intent.
        await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var since = (DateTime.UtcNow - _lastRequest).TotalMilliseconds;
            if (since < _minIntervalMs)
            {
                var delay = (int)Math.Ceiling(_minIntervalMs - since);
                // Delay CAN be cancelled; finally block ensures lock is always released.
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            _lastRequest = DateTime.UtcNow;
        }
        finally
        {
            _lock.Release();
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static double GetMinInterval(double requestsPerSecond) =>
        requestsPerSecond > 0 ? 1000.0 / requestsPerSecond : 1000.0;
}
