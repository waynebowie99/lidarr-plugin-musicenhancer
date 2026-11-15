using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using NLog;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

public interface IExplicitLookupService
{
    AdvisoryRating? Lookup(Track track);
}

public enum AdvisoryRating { Explicit = 1, Clean = 2 }

/// <summary>
/// Resolve Lidarr's MusicBrainz recording ID to ISRCs, then check Deezer's
/// public catalog. False is not treated as proof that a track is a clean edit.
/// </summary>
public class ExplicitLookupService : IExplicitLookupService
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly object RequestLock = new();
    private static DateTime _nextMusicBrainzRequest = DateTime.MinValue;
    private readonly Dictionary<string, AdvisoryRating?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient _client;
    private readonly Logger _logger;
    private DateTime _retryAfter = DateTime.MinValue;

    public ExplicitLookupService(Logger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = Client;
    }

    internal ExplicitLookupService(Logger logger, HttpClient client) : this(logger)
    {
        _client = client;
    }

    public AdvisoryRating? Lookup(Track track)
    {
        if (!Guid.TryParse(track.ForeignRecordingId, out var recordingId) || track.Duration <= 0)
        {
            return null;
        }

        // Include duration: the same recording ID may be attached to a different file.
        var cacheKey = $"{recordingId:D}:{track.Duration}";
        lock (RequestLock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            if (DateTime.UtcNow < _retryAfter)
            {
                return null;
            }

            try
            {
                var wait = _nextMusicBrainzRequest - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                {
                    Thread.Sleep(wait);
                }

                _nextMusicBrainzRequest = DateTime.UtcNow.AddMilliseconds(1100);
                using var recording = GetJson($"https://musicbrainz.org/ws/2/recording/{recordingId:D}?inc=isrcs&fmt=json");
                var root = recording.RootElement;
                if (!root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String ||
                    !Guid.TryParse(id.GetString(), out var actualId) || actualId != recordingId ||
                    !root.TryGetProperty("length", out var length) || !length.TryGetInt32(out var lengthMs) ||
                    lengthMs <= 0 || Math.Abs((long)lengthMs - track.Duration) > 5000)
                {
                    return null;
                }

                // MusicBrainz sometimes explicitly distinguishes the recording itself.
                // Do not infer either rating from an absent or unrelated disambiguation.
                if (root.TryGetProperty("disambiguation", out var note) && note.ValueKind == JsonValueKind.String)
                {
                    var description = (note.GetString() ?? string.Empty).Trim();
                    if (Regex.IsMatch(description, @"^(explicit|explicit version|explicit edit)$", RegexOptions.IgnoreCase))
                    {
                        return _cache[cacheKey] = AdvisoryRating.Explicit;
                    }
                    if (Regex.IsMatch(description, @"^(clean|clean version|clean edit)$", RegexOptions.IgnoreCase))
                    {
                        return _cache[cacheKey] = AdvisoryRating.Clean;
                    }
                }

                if (!root.TryGetProperty("isrcs", out var isrcs) || isrcs.ValueKind != JsonValueKind.Array)
                {
                    _cache[cacheKey] = null;
                    return null;
                }

                var codes = isrcs.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString() ?? string.Empty)
                    .Where(x => Regex.IsMatch(x, "^[A-Za-z0-9]{12}$"))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                // Large or conflicting identifier sets are ambiguous; never guess.
                if (codes.Count == 0 || codes.Count > 5)
                {
                    _cache[cacheKey] = null;
                    return null;
                }

                var ratings = new List<bool>();
                foreach (var code in codes)
                {
                    using var response = GetJson($"https://api.deezer.com/track/isrc:{code}");
                    var item = response.RootElement;
                    if (item.TryGetProperty("error", out _))
                    {
                        // Missing or rate-limited catalog entries cannot establish consensus.
                        return null;
                    }

                    if (!item.TryGetProperty("isrc", out var actualCode) || actualCode.ValueKind != JsonValueKind.String ||
                        !string.Equals(actualCode.GetString(), code, StringComparison.OrdinalIgnoreCase) ||
                        !item.TryGetProperty("duration", out var duration) || !duration.TryGetInt32(out var seconds) ||
                        seconds <= 0 || Math.Abs((long)seconds * 1000 - track.Duration) > 5000 ||
                        !item.TryGetProperty("explicit_lyrics", out var rating) ||
                        (rating.ValueKind != JsonValueKind.True && rating.ValueKind != JsonValueKind.False))
                    {
                        // A mismatched response makes the recording ambiguous.
                        return null;
                    }

                    ratings.Add(rating.GetBoolean());
                }

                AdvisoryRating? result = ratings.Count > 0 && ratings.All(x => x) ? AdvisoryRating.Explicit : null;
                _cache[cacheKey] = result;
                return result;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
            {
                // Don't cache transient errors, and never turn a failed lookup into "clean".
                _logger.Debug(ex, "MusicEnhancer: Explicit lookup failed for recording {0}", recordingId);
                _retryAfter = DateTime.UtcNow.AddMinutes(2);
                return null;
            }
        }
    }

    private JsonDocument GetJson(string url)
    {
        using var response = _client.GetAsync(url).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(response.Content.ReadAsStream());
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Lidarr-MusicEnhancer/1.0 (https://github.com/waynebowie99/lidarr-plugin-musicenhancer)");
        return client;
    }
}
