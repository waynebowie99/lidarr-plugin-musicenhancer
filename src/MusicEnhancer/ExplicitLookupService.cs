using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

public interface IExplicitLookupService
{
    AdvisoryRating? Lookup(Track track);
    AdvisoryRating? Lookup(Track track, RecordingMetadata? metadata, bool recordingLookupAttempted = false);
}

public enum AdvisoryRating { Explicit = 1, Clean = 2 }

/// <summary>
/// Resolve Lidarr's MusicBrainz recording ID to ISRCs, then check Deezer's
/// public catalog. False is not treated as proof that a track is a clean edit.
/// </summary>
public class ExplicitLookupService : IExplicitLookupService
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly object CacheLock = new();
    private static readonly object DeezerThrottleLock = new();
    private static DateTime _nextDeezerRequest = DateTime.MinValue;
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

    private static void WaitForMusicBrainz()
    {
        MusicBrainzThrottle.Wait();
    }

    public AdvisoryRating? Lookup(Track track)
    {
        if (!Guid.TryParse(track.ForeignRecordingId, out var recordingId) || track.Duration <= 0)
        {
            return null;
        }

        // Include duration: the same recording ID may be attached to a different file.
        var cacheKey = $"{recordingId:D}:{track.Duration}";
        lock (CacheLock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;
            if (DateTime.UtcNow < _retryAfter) return null;
        }

        try
        {
            WaitForMusicBrainz();
            using var recording = GetJson($"https://musicbrainz.org/ws/2/recording/{recordingId:D}?inc=isrcs&fmt=json");
            var root = recording.RootElement;
            if (!root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(id.GetString(), out var actualId) || actualId != recordingId ||
                !root.TryGetProperty("length", out var length) || !length.TryGetInt32(out var lengthMs) ||
                lengthMs <= 0 || Math.Abs((long)lengthMs - track.Duration) > 5000)
            {
                return Cache(cacheKey, null);
            }

            // MusicBrainz sometimes explicitly distinguishes the recording itself.
            if (root.TryGetProperty("disambiguation", out var note) && note.ValueKind == JsonValueKind.String)
            {
                var description = (note.GetString() ?? string.Empty).Trim();
                if (Regex.IsMatch(description, @"^(explicit|explicit version|explicit edit)$", RegexOptions.IgnoreCase))
                    return Cache(cacheKey, AdvisoryRating.Explicit);
                if (Regex.IsMatch(description, @"^(clean|clean version|clean edit)$", RegexOptions.IgnoreCase))
                    return Cache(cacheKey, AdvisoryRating.Clean);
            }

            var codes = root.TryGetProperty("isrcs", out var isrcs) && isrcs.ValueKind == JsonValueKind.Array
                ? isrcs.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? string.Empty)
                    .Where(x => Regex.IsMatch(x, "^[A-Za-z0-9]{12}$")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                : Array.Empty<string>();
            return Cache(cacheKey, LookupByIsrcs(track, codes));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            _logger.Debug(ex, "MusicEnhancer: Explicit lookup failed for recording {0}", recordingId);
            lock (CacheLock) _retryAfter = DateTime.UtcNow.AddMinutes(2);
            return null;
        }
    }

    public AdvisoryRating? Lookup(Track track, RecordingMetadata? metadata, bool recordingLookupAttempted = false)
    {
        if (!Guid.TryParse(track.ForeignRecordingId, out var recordingId) || track.Duration <= 0) return null;
        var cacheKey = $"{recordingId:D}:{track.Duration}";
        lock (CacheLock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;
        }
        if (metadata?.AdvisoryRating != null) return Cache(cacheKey, metadata.AdvisoryRating);
        if (metadata?.Isrcs?.Length > 0)
        {
            try
            {
                return Cache(cacheKey, LookupByIsrcs(track, metadata.Isrcs));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
            {
                _logger.Debug(ex, "MusicEnhancer: Explicit lookup failed for recording {0}", recordingId);
                lock (CacheLock) _retryAfter = DateTime.UtcNow.AddMinutes(2);
                return null;
            }
        }
        if (recordingLookupAttempted) return Cache(cacheKey, null);
        return Lookup(track);
    }

    private AdvisoryRating? LookupByIsrcs(Track track, IReadOnlyCollection<string> codes)
    {
        if (codes.Count == 0 || codes.Count > 5) return null;
        var ratings = new List<bool>();
        foreach (var code in codes)
        {
            WaitForDeezer();
            using var response = GetJson($"https://api.deezer.com/track/isrc:{code}");
            var item = response.RootElement;
            if (item.TryGetProperty("error", out _)) return null;
            if (!item.TryGetProperty("isrc", out var actualCode) || actualCode.ValueKind != JsonValueKind.String ||
                !string.Equals(actualCode.GetString(), code, StringComparison.OrdinalIgnoreCase) ||
                !item.TryGetProperty("duration", out var duration) || !duration.TryGetInt32(out var seconds) ||
                seconds <= 0 || Math.Abs((long)seconds * 1000 - track.Duration) > 5000 ||
                !item.TryGetProperty("explicit_lyrics", out var rating) ||
                (rating.ValueKind != JsonValueKind.True && rating.ValueKind != JsonValueKind.False)) return null;
            ratings.Add(rating.GetBoolean());
        }
        return ratings.Count > 0 && ratings.All(x => x) ? AdvisoryRating.Explicit : null;
    }

    private AdvisoryRating? Cache(string key, AdvisoryRating? value)
    {
        lock (CacheLock) _cache[key] = value;
        return value;
    }

    private static void WaitForDeezer()
    {
        lock (DeezerThrottleLock)
        {
            var delay = _nextDeezerRequest - DateTime.UtcNow;
            if (delay > TimeSpan.Zero) System.Threading.Thread.Sleep(delay);
            _nextDeezerRequest = DateTime.UtcNow.AddMilliseconds(500);
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
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 4 * 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Lidarr-MusicEnhancer/1.0 (https://github.com/waynebowie99/lidarr-plugin-musicenhancer)");
        return client;
    }
}
