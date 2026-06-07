using System.Net;
using System.Text.Json;
using BeenThere.Core.Interfaces;
using BeenThere.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BeenThere.Infrastructure.Services;

/// <summary>
/// Geocoding service using OpenStreetMap Nominatim API.
/// Handles rate limiting, retries, and caching via primary constructor.
/// </summary>
public class NominatimGeocodingService(
    HttpClient http,
    IMemoryCache cache,
    IOptions<NominatimOptions> options,
    ILogger<NominatimGeocodingService> logger) : IGeocodingService
{
    private static readonly Action<ILogger, Exception?> FailedSetUserAgent =
        LoggerMessage.Define(LogLevel.Warning, new(1000, "FailedUserAgent"), "Failed to set User-Agent header from options");

    private static readonly Action<ILogger, int, Exception?> RateLimited =
        LoggerMessage.Define<int>(LogLevel.Warning, new(1001, "NominatimRateLimited"), "Nominatim rate limited, retrying after {RetryAfter}s");

    private static readonly Action<ILogger, string, Exception?> GeocodeAttemptFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new(1002, "GeocodeAttemptFailed"), "Geocode attempt failed for query {Query}");

    private static readonly Action<ILogger, string, int, Exception?> GeocodeAttemptStarted =
        LoggerMessage.Define<string, int>(LogLevel.Debug, new(1003, "GeocodeStarted"), "Starting geocode request for query '{Query}' (maxResults: {MaxResults})");

    private static readonly Action<ILogger, string, int, int, Exception?> GeocodeAttemptSucceeded =
        LoggerMessage.Define<string, int, int>(LogLevel.Debug, new(1004, "GeocodeSucceeded"), "Geocode request succeeded for query '{Query}' (returned {ResultCount} results, attempt {Attempt})");

    private static readonly Action<ILogger, string, Exception?> GeocodeCacheHit =
        LoggerMessage.Define<string>(LogLevel.Debug, new(1005, "GeocodeCacheHit"), "Geocode cache hit for '{Query}'");

    private static readonly Action<ILogger, string, Exception?> UserAgentSet =
        LoggerMessage.Define<string>(LogLevel.Debug, new(1006, "UserAgentSet"), "User-Agent header set to: {UserAgent}");

    private static readonly Action<ILogger, Exception?> UserAgentNotConfigured =
        LoggerMessage.Define(LogLevel.Warning, new(1007, "NoUserAgent"), "No User-Agent configured for Nominatim requests (check appsettings.json Nominatim:UserAgent)");

    private readonly NominatimOptions _opts = options.Value;

    static NominatimGeocodingService()
    {
        // This runs once when the type is first referenced; we can't use non-static context here
    }

    // Set up User-Agent header from options if configured
    private void InitializeUserAgent()
    {
        if (!string.IsNullOrEmpty(_opts.UserAgent))
        {
            if (!http.DefaultRequestHeaders.UserAgent.TryParseAdd(_opts.UserAgent))
            {
                FailedSetUserAgent(logger, null);
            }
            else
            {
                UserAgentSet(logger, _opts.UserAgent, null);
            }
        }
        else
        {
            UserAgentNotConfigured(logger, null);
        }
    }

    public async Task<IEnumerable<GeocodeResult>> GeocodeAsync(string query, int maxResults = 7, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        // Initialize User-Agent on first call
        InitializeUserAgent();

        var key = $"geocode:{query}:{maxResults}";
        if (cache.TryGetValue<IEnumerable<GeocodeResult>>(key, out var cached) && cached is not null)
        {
            GeocodeCacheHit(logger, query, null);
            return cached;
        }

        var attempt = 0;
        var maxAttempts = Math.Max(1, _opts.MaxRetries);

        while (true)
        {
            attempt++;
            try
            {
                var url = $"search?format=json&q={Uri.EscapeDataString(query)}&limit={maxResults}";
                GeocodeAttemptStarted(logger, query, maxResults, null);
                var resp = await http.GetAsync(url, ct).ConfigureAwait(false);

                if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (attempt >= maxAttempts)
                    {
                        break;
                    }

                    var retryAfter = ParseRetryAfter(resp) ?? (int)Math.Pow(2, attempt);
                    RateLimited(logger, retryAfter, null);
                    await Task.Delay(TimeSpan.FromSeconds(retryAfter), ct).ConfigureAwait(false);
                    continue;
                }

                resp.EnsureSuccessStatusCode();

                using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                var docs = await JsonSerializer.DeserializeAsync<JsonElement[]>(stream, cancellationToken: ct).ConfigureAwait(false) ?? [];
                var results = docs.Select(Map).OfType<GeocodeResult>().ToList();
                GeocodeAttemptSucceeded(logger, query, results.Count, attempt, null);

                cache.Set(key, results, TimeSpan.FromSeconds(_opts.CacheTtlSeconds));
                return results;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                GeocodeAttemptFailed(logger, query, ex);
                if (attempt >= maxAttempts)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct).ConfigureAwait(false);
            }
        }

        return [];
    }

    private static int? ParseRetryAfter(HttpResponseMessage resp) =>
        resp.Headers.TryGetValues("Retry-After", out var vals)
            ? vals.FirstOrDefault() switch
            {
                null => null,
                var v when int.TryParse(v, out var secs) => secs,
                var v when DateTimeOffset.TryParse(v, out var dto) => Math.Max(1, (int)(dto - DateTimeOffset.UtcNow).TotalSeconds),
                _ => null
            }
            : null;

    private static GeocodeResult? Map(JsonElement el)
    {
        try
        {
            var nc = System.Globalization.CultureInfo.InvariantCulture;
            var nf = System.Globalization.NumberStyles.Float;
            var lat = el.GetProperty("lat").GetString();
            var lon = el.GetProperty("lon").GetString();

            if (!double.TryParse(lat, nf, nc, out var latd) || !double.TryParse(lon, nf, nc, out var lond))
            {
                return null;
            }

            var name = el.TryGetProperty("display_name", out var dn) ? dn.GetString() : null;

            double? importance = el.TryGetProperty("importance", out var imp) && imp.ValueKind == JsonValueKind.Number && imp.TryGetDouble(out var impd)
                ? impd
                : null;

            double[]? bbox = el.TryGetProperty("boundingbox", out var bb) && bb.ValueKind == JsonValueKind.Array
                ? [.. bb.EnumerateArray().Select(ParseBboxValue).OfType<double>()]
                : null;

            return new() { Lat = latd, Lng = lond, DisplayName = name, Importance = importance, BoundingBox = bbox };
        }
        catch
        {
            return null;
        }
    }

    private static double? ParseBboxValue(JsonElement el)
    {
        var nc = System.Globalization.CultureInfo.InvariantCulture;
        var nf = System.Globalization.NumberStyles.Float;
        return el.ValueKind switch
        {
            JsonValueKind.String when double.TryParse(el.GetString(), nf, nc, out var v) => v,
            JsonValueKind.Number when el.TryGetDouble(out var v) => v,
            _ => null
        };
    }
}
