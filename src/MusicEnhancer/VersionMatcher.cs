using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

/// <summary>
/// Core matching engine that identifies version types for tracks.
/// Implements version detection: remixes, edits, live versions, acoustic versions, etc.
/// </summary>
public static class VersionMatcher
{
    /// <summary>
    /// Duration tolerance in milliseconds for matching (±3 seconds).
    /// </summary>
    public const int DurationToleranceMs = 3000;

    /// <summary>
    /// Confidence values for each version type.
    /// </summary>
    public static class Confidence
    {
        public const double Remix = 0.95;
        public const double Edit = 0.90;
        public const double Version = 0.85;
        public const double Live = 0.80;
        public const double Acoustic = 0.80;
        public const double Demo = 0.75;
        public const double NoMatch = 0.0;
    }

    /// <summary>
    /// Check all tracks in an album for version matches.
    /// </summary>
    /// <param name="albumTracks">Tracks from the album to check.</param>
    /// <param name="comparisonTracks">All comparison tracks from other albums.</param>
    /// <param name="options">Filtering options.</param>
    /// <returns>List of version match results for each track.</returns>
    public static List<VersionMatchResult> CheckVersions(List<Track> albumTracks, IEnumerable<Track> comparisonTracks, ContentFilterOptions options)
    {
        if (albumTracks == null || albumTracks.Count == 0)
        {
            return new List<VersionMatchResult>();
        }

        if (comparisonTracks == null)
        {
            return albumTracks.Select(t => new VersionMatchResult(t, null, VersionType.None, Confidence.NoMatch, "No comparison tracks available.")).ToList();
        }

        var comparisonList = comparisonTracks.ToList();

        return albumTracks.Select(track => FindBestVersionMatch(track, comparisonList, options)).ToList();
    }

    /// <summary>
    /// Find the best version match for a single track.
    /// </summary>
    /// <param name="track">The track to check.</param>
    /// <param name="comparisonTracks">All comparison tracks.</param>
    /// <param name="options">Filtering options.</param>
    /// <returns>The best version match result.</returns>
    public static VersionMatchResult FindBestVersionMatch(Track track, IEnumerable<Track> comparisonTracks, ContentFilterOptions options)
    {
        if (track == null)
        {
            throw new ArgumentNullException(nameof(track));
        }

        if (comparisonTracks == null)
        {
            return new VersionMatchResult(track, null, VersionType.None, Confidence.NoMatch, "No comparison tracks available.");
        }

        // Check for remix matches
        if (options.EnableRemixFiltering)
        {
            var remixMatch = FindRemixMatch(track, comparisonTracks);
            if (remixMatch != null)
            {
                return remixMatch;
            }
        }

        // Check for edit matches
        if (options.EnableEditVersionFiltering)
        {
            var editMatch = FindEditMatch(track, comparisonTracks);
            if (editMatch != null)
            {
                return editMatch;
            }
        }

        // Check for version matches
        if (options.EnableVersionFiltering)
        {
            var versionMatch = FindVersionMatch(track, comparisonTracks);
            if (versionMatch != null)
            {
                return versionMatch;
            }
        }

        // Check for live version matches
        if (options.EnableLiveVersionFiltering)
        {
            var liveMatch = FindLiveMatch(track, comparisonTracks);
            if (liveMatch != null)
            {
                return liveMatch;
            }
        }

        // Check for acoustic matches
        if (options.EnableAcousticVersionFiltering)
        {
            var acousticMatch = FindAcousticMatch(track, comparisonTracks);
            if (acousticMatch != null)
            {
                return acousticMatch;
            }
        }

        // Check for demo matches
        if (options.EnableDemoVersionFiltering)
        {
            var demoMatch = FindDemoMatch(track, comparisonTracks);
            if (demoMatch != null)
            {
                return demoMatch;
            }
        }

        // No match found
        return new VersionMatchResult(track, null, VersionType.None, Confidence.NoMatch, "No version match found.");
    }

    /// <summary>
    /// Find remix matches (Remix, Edit, Bootleg, Reinterpretation, etc.)
    /// </summary>
    private static VersionMatchResult? FindRemixMatch(Track track, IEnumerable<Track> comparisonTracks)
    {
        var normalizedTitle = TitleNormalizer.Normalize(track.Title);

        // Check if track title contains remix indicators
        if (IsRemixTitle(normalizedTitle))
        {
            // Find the original version
            var originalTitle = RemoveRemixKeywords(normalizedTitle);
            var originalTrack = comparisonTracks
                .Where(t => !IsRemixTitle(TitleNormalizer.Normalize(t.Title)))
                .FirstOrDefault(t => TitleNormalizer.Normalize(t.Title) == originalTitle);

            if (originalTrack != null)
            {
                return new VersionMatchResult(
                    track,
                    originalTrack,
                    VersionType.Remix,
                    Confidence.Remix,
                    $"Remix version detected: '{track.Title}' matches original '{originalTrack.Title}'");
            }

            // Try to find by duration match
            var durationMatch = comparisonTracks
                .Where(t => !IsRemixTitle(TitleNormalizer.Normalize(t.Title)))
                .FirstOrDefault(t => 
                    TitleNormalizer.Normalize(t.Title) == originalTitle ||
                    Math.Abs(t.Duration - track.Duration) <= DurationToleranceMs);

            if (durationMatch != null)
            {
                return new VersionMatchResult(
                    track,
                    durationMatch,
                    VersionType.Remix,
                    Confidence.Remix,
                    $"Remix version detected by duration: '{track.Title}' (duration diff: {Math.Abs(track.Duration - durationMatch.Duration)}ms)");
            }
        }

        return null;
    }

    /// <summary>
    /// Find edit matches (Edit, Radio Edit, Album Edit, etc.)
    /// </summary>
    private static VersionMatchResult? FindEditMatch(Track track, IEnumerable<Track> comparisonTracks)
    {
        var normalizedTitle = TitleNormalizer.Normalize(track.Title);

        // Check if track title contains edit indicators
        if (IsEditTitle(normalizedTitle))
        {
            // Find the original version
            var originalTitle = RemoveEditKeywords(normalizedTitle);
            var originalTrack = comparisonTracks
                .Where(t => !IsEditTitle(TitleNormalizer.Normalize(t.Title)))
                .FirstOrDefault(t => TitleNormalizer.Normalize(t.Title) == originalTitle);

            if (originalTrack != null)
            {
                return new VersionMatchResult(
                    track,
                    originalTrack,
                    VersionType.Edit,
                    Confidence.Edit,
                    $"Edit version detected: '{track.Title}' matches original '{originalTrack.Title}'");
            }

            // Try to find by duration match
            var durationMatch = comparisonTracks
                .Where(t => !IsEditTitle(TitleNormalizer.Normalize(t.Title)))
                .FirstOrDefault(t => 
                    TitleNormalizer.Normalize(t.Title) == originalTitle ||
                    Math.Abs(t.Duration - track.Duration) <= DurationToleranceMs);

            if (durationMatch != null)
            {
                return new VersionMatchResult(
                    track,
                    durationMatch,
                    VersionType.Edit,
                    Confidence.Edit,
                    $"Edit version detected by duration: '{track.Title}' (duration diff: {Math.Abs(track.Duration - durationMatch.Duration)}ms)");
            }
        }

        return null;
    }

    /// <summary>
    /// Find version matches (Version, Alternative Version, etc.)
    /// </summary>
    private static VersionMatchResult? FindVersionMatch(Track track, IEnumerable<Track> comparisonTracks)
    {
        var normalizedTitle = TitleNormalizer.Normalize(track.Title);

        // Check if track title contains version indicators
        if (IsVersionTitle(normalizedTitle))
        {
            // Find the original version
            var originalTitle = RemoveVersionKeywords(normalizedTitle);
            var originalTrack = comparisonTracks
                .Where(t => !IsVersionTitle(TitleNormalizer.Normalize(t.Title)))
                .FirstOrDefault(t => TitleNormalizer.Normalize(t.Title) == originalTitle);

            if (originalTrack != null)
            {
                return new VersionMatchResult(
                    track,
                    originalTrack,
                    VersionType.Version,
                    Confidence.Version,
                    $"Version detected: '{track.Title}' matches original '{originalTrack.Title}'");
            }

            // Try to find by duration match
            var durationMatch = comparisonTracks
                .Where(t => !IsVersionTitle(TitleNormalizer.Normalize(t.Title)))
                .FirstOrDefault(t => 
                    TitleNormalizer.Normalize(t.Title) == originalTitle ||
                    Math.Abs(t.Duration - track.Duration) <= DurationToleranceMs);

            if (durationMatch != null)
            {
                return new VersionMatchResult(
                    track,
                    durationMatch,
                    VersionType.Version,
                    Confidence.Version,
                    $"Version detected by duration: '{track.Title}' (duration diff: {Math.Abs(track.Duration - durationMatch.Duration)}ms)");
            }
        }

        return null;
    }

    /// <summary>
    /// Find live version matches (Live, Live Version, Live at, etc.)
    /// </summary>
    private static VersionMatchResult? FindLiveMatch(Track track, IEnumerable<Track> comparisonTracks)
    {
        var normalizedTitle = TitleNormalizer.Normalize(track.Title);

        // Check if track title contains live indicators
        if (IsLiveTitle(normalizedTitle))
        {
            // Find the studio version
            var originalTitle = RemoveLiveKeywords(normalizedTitle);
            var studioTrack = comparisonTracks
                .Where(t => !IsLiveTitle(TitleNormalizer.Normalize(t.Title)))
                .FirstOrDefault(t => TitleNormalizer.Normalize(t.Title) == originalTitle);

            if (studioTrack != null)
            {
                return new VersionMatchResult(
                    track,
                    studioTrack,
                    VersionType.Live,
                    Confidence.Live,
                    $"Live version detected: '{track.Title}' matches studio '{studioTrack.Title}'");
            }

            // Try to find by duration match
            var durationMatch = comparisonTracks
                .Where(t => !IsLiveTitle(TitleNormalizer.Normalize(t.Title)))
                .FirstOrDefault(t => 
                    TitleNormalizer.Normalize(t.Title) == originalTitle ||
                    Math.Abs(t.Duration - track.Duration) <= DurationToleranceMs);

            if (durationMatch != null)
            {
                return new VersionMatchResult(
                    track,
                    durationMatch,
                    VersionType.Live,
                    Confidence.Live,
                    $"Live version detected by duration: '{track.Title}' (duration diff: {Math.Abs(track.Duration - durationMatch.Duration)}ms)");
            }
        }

        return null;
    }

    /// <summary>
    /// Find acoustic version matches (Acoustic, Acoustic Version, etc.)
    /// </summary>
    private static VersionMatchResult? FindAcousticMatch(Track track, IEnumerable<Track> comparisonTracks)
    {
        var normalizedTitle = TitleNormalizer.Normalize(track.Title);

        // Check if track title contains acoustic indicators
        if (IsAcousticTitle(normalizedTitle))
        {
            // Find the original version
            var originalTitle = RemoveAcousticKeywords(normalizedTitle);
            var originalTrack = comparisonTracks
                .Where(t => !IsAcousticTitle(TitleNormalizer.Normalize(t.Title)))
                .FirstOrDefault(t => TitleNormalizer.Normalize(t.Title) == originalTitle);

            if (originalTrack != null)
            {
                return new VersionMatchResult(
                    track,
                    originalTrack,
                    VersionType.Acoustic,
                    Confidence.Acoustic,
                    $"Acoustic version detected: '{track.Title}' matches original '{originalTrack.Title}'");
            }

            // Try to find by duration match
            var durationMatch = comparisonTracks
                .Where(t => !IsAcousticTitle(TitleNormalizer.Normalize(t.Title)))
                .FirstOrDefault(t => 
                    TitleNormalizer.Normalize(t.Title) == originalTitle ||
                    Math.Abs(t.Duration - track.Duration) <= DurationToleranceMs);

            if (durationMatch != null)
            {
                return new VersionMatchResult(
                    track,
                    durationMatch,
                    VersionType.Acoustic,
                    Confidence.Acoustic,
                    $"Acoustic version detected by duration: '{track.Title}' (duration diff: {Math.Abs(track.Duration - durationMatch.Duration)}ms)");
            }
        }

        return null;
    }

    /// <summary>
    /// Find demo matches (Demo, Demo Version, etc.)
    /// </summary>
    private static VersionMatchResult? FindDemoMatch(Track track, IEnumerable<Track> comparisonTracks)
    {
        var normalizedTitle = TitleNormalizer.Normalize(track.Title);

        // Check if track title contains demo indicators
        if (IsDemoTitle(normalizedTitle))
        {
            // Find the original version
            var originalTitle = RemoveDemoKeywords(normalizedTitle);
            var originalTrack = comparisonTracks
                .Where(t => !IsDemoTitle(TitleNormalizer.Normalize(t.Title)))
                .FirstOrDefault(t => TitleNormalizer.Normalize(t.Title) == originalTitle);

            if (originalTrack != null)
            {
                return new VersionMatchResult(
                    track,
                    originalTrack,
                    VersionType.Demo,
                    Confidence.Demo,
                    $"Demo version detected: '{track.Title}' matches original '{originalTrack.Title}'");
            }

            // Try to find by duration match
            var durationMatch = comparisonTracks
                .Where(t => !IsDemoTitle(TitleNormalizer.Normalize(t.Title)))
                .FirstOrDefault(t => 
                    TitleNormalizer.Normalize(t.Title) == originalTitle ||
                    Math.Abs(t.Duration - track.Duration) <= DurationToleranceMs);

            if (durationMatch != null)
            {
                return new VersionMatchResult(
                    track,
                    durationMatch,
                    VersionType.Demo,
                    Confidence.Demo,
                    $"Demo version detected by duration: '{track.Title}' (duration diff: {Math.Abs(track.Duration - durationMatch.Duration)}ms)");
            }
        }

        return null;
    }

    #region Title Pattern Detection

    private static bool IsRemixTitle(string title)
    {
        var patterns = new[] {
            @"remix", @"edit", @"bootleg", @"reinterpretation", @"rework", @"remake",
            @"reimagined", @"reinterpreted", @"remastered", @"extended mix", @"club mix",
            @"dance mix", @"house mix", @"techno mix", @"trance mix", @"dub mix",
            @"instrumental", @"flip", @"rework"
        };

        return patterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(title, p, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    private static bool IsEditTitle(string title)
    {
        var patterns = new[] {
            @"edit", @"radio edit", @"album edit", @"single edit", @"short edit",
            @"compact edit", @"clean edit", @"intro edit", @"outro edit", @"cut",
            @"short version", @"long version", @"extended", @"extended version"
        };

        return patterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(title, p, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    private static bool IsVersionTitle(string title)
    {
        var patterns = new[] {
            @"version", @"alternative version", @"alternate version", @"different version",
            @"special version", @"unique version", @"exclusive version"
        };

        return patterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(title, p, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    private static bool IsLiveTitle(string title)
    {
        var patterns = new[] {
            @"live", @"live version", @"live at", @"live from", @"live in", @"live on",
            @"acoustic live", @"electric live", @"unplugged"
        };

        return patterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(title, p, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    private static bool IsAcousticTitle(string title)
    {
        var patterns = new[] {
            @"acoustic", @"acoustic version", @"acoustic version", @"acoustic version",
            @"unplugged", @"acoustic version"
        };

        return patterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(title, p, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    private static bool IsDemoTitle(string title)
    {
        var patterns = new[] {
            @"demo", @"demo version", @"demo recording", @"demo session", @"demo take"
        };

        return patterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(title, p, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    #endregion

    #region Title Modification

    private static string RemoveRemixKeywords(string title)
    {
        var patterns = new[] {
            @"\s*\(remix\)", @"\s*\[remix\]", @"\s*remix", @"\s*\(edit\)", @"\s*\[edit\]",
            @"\s*edit", @"\s*\(bootleg\)", @"\s*\[bootleg\]", @"\s*bootleg",
            @"\s*\(reinterpretation\)", @"\s*\[reinterpretation\]", @"\s*reinterpretation",
            @"\s*\(rework\)", @"\s*\[rework\]", @"\s*rework", @"\s*\(remake\)", @"\s*\[remake\]",
            @"\s*remake", @"\s*\(reimagined\)", @"\s*\[reimagined\]", @"\s*reimagined",
            @"\s*\(reinterpreted\)", @"\s*\[reinterpreted\]", @"\s*reinterpreted",
            @"\s*\(remastered\)", @"\s*\[remastered\]", @"\s*remastered",
            @"\s*\(extended mix\)", @"\s*\[extended mix\]", @"\s*extended mix",
            @"\s*\(club mix\)", @"\s*\[club mix\]", @"\s*club mix",
            @"\s*\(dance mix\)", @"\s*\[dance mix\]", @"\s*dance mix",
            @"\s*\(house mix\)", @"\s*\[house mix\]", @"\s*house mix",
            @"\s*\(techno mix\)", @"\s*\[techno mix\]", @"\s*techno mix",
            @"\s*\(trance mix\)", @"\s*\[trance mix\]", @"\s*trance mix",
            @"\s*\(dub mix\)", @"\s*\[dub mix\]", @"\s*dub mix",
            @"\s*\(instrumental\)", @"\s*\[instrumental\]", @"\s*instrumental",
            @"\s*\(flip\)", @"\s*\[flip\]", @"\s*flip", @"\s*\(rework\)", @"\s*\[rework\]",
            @"\s*rework"
        };

        var result = title;
        foreach (var pattern in patterns)
        {
            result = System.Text.RegularExpressions.Regex.Replace(result, pattern, "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        // Clean up extra spaces
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();

        return result;
    }

    private static string RemoveEditKeywords(string title)
    {
        var patterns = new[] {
            @"\s*\(edit\)", @"\s*\[edit\]", @"\s*edit", @"\s*\(radio edit\)", @"\s*\[radio edit\]",
            @"\s*radio edit", @"\s*\(album edit\)", @"\s*\[album edit\]", @"\s*album edit",
            @"\s*\(single edit\)", @"\s*\[single edit\]", @"\s*single edit", @"\s*\(short edit\)",
            @"\s*\[short edit\]", @"\s*short edit", @"\s*\(compact edit\)", @"\s*\[compact edit\]",
            @"\s*compact edit", @"\s*\(clean edit\)", @"\s*\[clean edit\]", @"\s*clean edit",
            @"\s*\(intro edit\)", @"\s*\[intro edit\]", @"\s*intro edit", @"\s*\(outro edit\)",
            @"\s*\[outro edit\]", @"\s*outro edit", @"\s*\(cut\)", @"\s*\[cut\]", @"\s*cut",
            @"\s*\(short version\)", @"\s*\[short version\]", @"\s*short version",
            @"\s*\(long version\)", @"\s*\[long version\]", @"\s*long version",
            @"\s*\(extended\)", @"\s*\[extended\]", @"\s*extended", @"\s*\(extended version\)",
            @"\s*\[extended version\]", @"\s*extended version"
        };

        var result = title;
        foreach (var pattern in patterns)
        {
            result = System.Text.RegularExpressions.Regex.Replace(result, pattern, "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();

        return result;
    }

    private static string RemoveVersionKeywords(string title)
    {
        var patterns = new[] {
            @"\s*\(version\)", @"\s*\[version\]", @"\s*version", @"\s*\(alternative version\)",
            @"\s*\[alternative version\]", @"\s*alternative version", @"\s*\(alternate version\)",
            @"\s*\[alternate version\]", @"\s*alternate version", @"\s*\(different version\)",
            @"\s*\[different version\]", @"\s*different version", @"\s*\(special version\)",
            @"\s*\[special version\]", @"\s*special version", @"\s*\(unique version\)",
            @"\s*\[unique version\]", @"\s*unique version", @"\s*\(exclusive version\)",
            @"\s*\[exclusive version\]", @"\s*exclusive version"
        };

        var result = title;
        foreach (var pattern in patterns)
        {
            result = System.Text.RegularExpressions.Regex.Replace(result, pattern, "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();

        return result;
    }

    private static string RemoveLiveKeywords(string title)
    {
        var patterns = new[] {
            @"\s*\(live\)", @"\s*\[live\]", @"\s*live", @"\s*\(live version\)", @"\s*\[live version\]",
            @"\s*live version", @"\s*\(live at\)", @"\s*\[live at\]", @"\s*live at",
            @"\s*\(live from\)", @"\s*\[live from\]", @"\s*live from", @"\s*\(live in\)", @"\s*\[live in\]",
            @"\s*live in", @"\s*\(live on\)", @"\s*\[live on\]", @"\s*live on",
            @"\s*\(acoustic live\)", @"\s*\[acoustic live\]", @"\s*acoustic live",
            @"\s*\(electric live\)", @"\s*\[electric live\]", @"\s*electric live",
            @"\s*\(unplugged\)", @"\s*\[unplugged\]", @"\s*unplugged"
        };

        var result = title;
        foreach (var pattern in patterns)
        {
            result = System.Text.RegularExpressions.Regex.Replace(result, pattern, "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();

        return result;
    }

    private static string RemoveAcousticKeywords(string title)
    {
        var patterns = new[] {
            @"\s*\(acoustic\)", @"\s*\[acoustic\]", @"\s*acoustic", @"\s*\(acoustic version\)",
            @"\s*\[acoustic version\]", @"\s*acoustic version", @"\s*\(unplugged\)", @"\s*\[unplugged\]",
            @"\s*unplugged"
        };

        var result = title;
        foreach (var pattern in patterns)
        {
            result = System.Text.RegularExpressions.Regex.Replace(result, pattern, "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();

        return result;
    }

    private static string RemoveDemoKeywords(string title)
    {
        var patterns = new[] {
            @"\s*\(demo\)", @"\s*\[demo\]", @"\s*demo", @"\s*\(demo version\)", @"\s*\[demo version\]",
            @"\s*demo version", @"\s*\(demo recording\)", @"\s*\[demo recording\]", @"\s*demo recording",
            @"\s*\(demo session\)", @"\s*\[demo session\]", @"\s*demo session", @"\s*\(demo take\)",
            @"\s*\[demo take\]", @"\s*demo take"
        };

        var result = title;
        foreach (var pattern in patterns)
        {
            result = System.Text.RegularExpressions.Regex.Replace(result, pattern, "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();

        return result;
    }

    #endregion
}
