using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using NLog;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

public interface IAdditionalMetadataLookupService
{
    uint? LookupBpm(string? isrc, int durationMs);
    string? LookupLyrics(string? artist, string? album, string? title, int durationMs);
    string[]? LookupComposers(Track track);
    string[]? LookupMoods(Track track, string? apiKey);
}

public class AdditionalMetadataLookupService : IAdditionalMetadataLookupService
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly object RequestGate = new();
    private static readonly Dictionary<string, DateTime> NextRequests = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, DateTime> RetryAfter = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient _client;
    private readonly Logger _logger;
    private static readonly HashSet<string> KnownMoods = new(StringComparer.OrdinalIgnoreCase)
    {
        "happy", "sad", "melancholic", "melancholy", "energetic", "relaxing", "calm", "chill",
        "romantic", "dark", "uplifting", "peaceful", "mellow", "dreamy", "aggressive", "angry"
    };

    public AdditionalMetadataLookupService(Logger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = Client;
    }

    internal AdditionalMetadataLookupService(Logger logger, HttpClient client) : this(logger) => _client = client;

    public string[]? LookupMoods(Track track, string? apiKey)
    {
        if (!Guid.TryParse(track.ForeignRecordingId, out var recordingId) || string.IsNullOrWhiteSpace(apiKey)) return null;
        try
        {
            using var json = GetJson($"https://ws.audioscrobbler.com/2.0/?method=track.gettoptags&mbid={recordingId:D}&api_key={Uri.EscapeDataString(apiKey)}&format=json");
            if (json == null || !json.RootElement.TryGetProperty("toptags", out var top) ||
                !top.TryGetProperty("tag", out var tags) || tags.ValueKind != JsonValueKind.Array) return null;
            var moods = new List<string>();
            foreach (var tag in tags.EnumerateArray().Take(30))
            {
                if (!tag.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String) continue;
                var value = name.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value) && KnownMoods.Contains(value)) moods.Add(value.ToLowerInvariant());
            }
            var result = moods.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return result.Length == 0 ? null : result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            // Do not log exceptions that might expose the API key in the URL.
            _logger.Debug("MusicEnhancer: Last.fm mood lookup failed for recording {0}", recordingId);
            return null;
        }
    }

    public string[]? LookupComposers(Track track)
    {
        if (!Guid.TryParse(track.ForeignRecordingId, out var recordingId) || track.Duration <= 0) return null;
        try
        {
            MusicBrainzThrottle.Wait();
            using var recording = GetJson($"https://musicbrainz.org/ws/2/recording/{recordingId:D}?inc=work-rels&fmt=json");
            if (recording == null) return null;
            var root = recording.RootElement;
            if (!root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(id.GetString(), out var actual) || actual != recordingId ||
                !root.TryGetProperty("length", out var length) || !length.TryGetInt32(out var lengthMs) ||
                lengthMs <= 0 || Math.Abs((long)lengthMs - track.Duration) > 5000 ||
                !root.TryGetProperty("relations", out var relationships) || relationships.ValueKind != JsonValueKind.Array) return null;

            var works = new HashSet<Guid>();
            foreach (var relation in relationships.EnumerateArray())
            {
                if (relation.TryGetProperty("type", out var type) && type.GetString() == "performance" &&
                    relation.TryGetProperty("work", out var work) && work.TryGetProperty("id", out var workId) &&
                    workId.ValueKind == JsonValueKind.String && Guid.TryParse(workId.GetString(), out var parsed)) works.Add(parsed);
            }
            if (works.Count == 0 || works.Count > 3) return null;

            var names = new List<string>();
            foreach (var workId in works)
            {
                MusicBrainzThrottle.Wait();
                using var work = GetJson($"https://musicbrainz.org/ws/2/work/{workId:D}?inc=artist-rels&fmt=json");
                if (work == null || !work.RootElement.TryGetProperty("id", out var fetchedId) ||
                    fetchedId.ValueKind != JsonValueKind.String || !Guid.TryParse(fetchedId.GetString(), out var fetched) || fetched != workId ||
                    !work.RootElement.TryGetProperty("relations", out var relations) || relations.ValueKind != JsonValueKind.Array) return null;
                foreach (var relation in relations.EnumerateArray())
                {
                    if (!relation.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "composer" ||
                        !relation.TryGetProperty("artist", out var artist) || !artist.TryGetProperty("name", out var name) ||
                        name.ValueKind != JsonValueKind.String) continue;
                    var value = relation.TryGetProperty("target-credit", out var credit) && credit.ValueKind == JsonValueKind.String
                        ? credit.GetString() : null;
                    if (string.IsNullOrWhiteSpace(value)) value = name.GetString();
                    if (!string.IsNullOrWhiteSpace(value)) names.Add(value.Trim());
                }
            }
            var unique = names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return unique.Length == 0 ? null : unique;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            _logger.Debug(ex, "MusicEnhancer: Composer lookup failed for recording {0}", recordingId);
            return null;
        }
    }

    public uint? LookupBpm(string? isrc, int durationMs)
    {
        if (isrc == null || !Regex.IsMatch(isrc, "^[A-Za-z0-9]{12}$") || durationMs <= 0) return null;
        try
        {
            using var json = GetJson($"https://api.deezer.com/track/isrc:{isrc}");
            if (json == null) return null;
            var root = json.RootElement;
            if (!root.TryGetProperty("isrc", out var code) || code.ValueKind != JsonValueKind.String ||
                !string.Equals(code.GetString(), isrc, StringComparison.OrdinalIgnoreCase) ||
                !root.TryGetProperty("duration", out var duration) || !duration.TryGetInt32(out var seconds) ||
                seconds <= 0 || Math.Abs((long)seconds * 1000 - durationMs) > 5000 ||
                !root.TryGetProperty("bpm", out var bpm) || !bpm.TryGetDouble(out var value) ||
                value < 30 || value > 300) return null;
            return (uint)Math.Round(value, MidpointRounding.AwayFromZero);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            _logger.Debug(ex, "MusicEnhancer: BPM lookup failed for ISRC {0}", isrc);
            return null;
        }
    }

    public string? LookupLyrics(string? artist, string? album, string? title, int durationMs)
    {
        if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(album) ||
            string.IsNullOrWhiteSpace(title) || durationMs <= 0) return null;
        try
        {
            var url = $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artist)}&album_name={Uri.EscapeDataString(album)}&track_name={Uri.EscapeDataString(title)}&duration={Math.Round(durationMs / 1000.0)}";
            using var json = GetJson(url);
            if (json == null) return null;
            var root = json.RootElement;
            if (!Matches(root, "artistName", artist) || !Matches(root, "albumName", album) || !Matches(root, "trackName", title) ||
                !root.TryGetProperty("duration", out var duration) || !duration.TryGetDouble(out var seconds) ||
                Math.Abs(seconds * 1000 - durationMs) > 3000 ||
                (root.TryGetProperty("instrumental", out var instrumental) && instrumental.ValueKind == JsonValueKind.True)) return null;

            foreach (var key in new[] { "syncedLyrics", "plainLyrics" })
            {
                if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var lyrics = value.GetString();
                    if (!string.IsNullOrWhiteSpace(lyrics) && lyrics.Length <= 256 * 1024) return lyrics;
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            _logger.Debug(ex, "MusicEnhancer: Lyrics lookup failed for track '{0}'", title);
        }
        return null;
    }

    private static bool Matches(JsonElement root, string key, string expected) =>
        root.TryGetProperty(key, out var item) && item.ValueKind == JsonValueKind.String &&
        string.Equals(item.GetString()?.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);

    private JsonDocument? GetJson(string url)
    {
        var host = new Uri(url).Host;
        lock (RequestGate)
        {
            if (RetryAfter.TryGetValue(host, out var retry) && DateTime.UtcNow < retry) return null;
            if (host != "musicbrainz.org" && NextRequests.TryGetValue(host, out var next))
            {
                var wait = next - DateTime.UtcNow;
                if (wait > TimeSpan.Zero) Thread.Sleep(wait);
            }
            if (host != "musicbrainz.org") NextRequests[host] = DateTime.UtcNow.AddMilliseconds(500);
        }

        try
        {
            using var response = _client.GetAsync(url).GetAwaiter().GetResult();
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return JsonDocument.Parse(response.Content.ReadAsStream());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            lock (RequestGate) RetryAfter[host] = DateTime.UtcNow.AddMinutes(2);
            throw;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Lidarr-MusicEnhancer/1.0 (https://github.com/waynebowie99/lidarr-plugin-musicenhancer)");
        return client;
    }
}
