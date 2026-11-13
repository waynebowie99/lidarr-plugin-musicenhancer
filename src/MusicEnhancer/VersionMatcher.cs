using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

/// <summary>
/// Service for matching tracks to version types.
/// </summary>
public static class VersionMatcher
{
    private static readonly Dictionary<VersionType, string[]> VersionPatterns = new Dictionary<VersionType, string[]>
    {
        { VersionType.Remix, new[] { "remix", "edit", "bootleg", "reinterpretation", "rework", "flip", "mashup", "remake" } },
        { VersionType.Edit, new[] { "edit", "radio edit", "album edit", "remaster", "remastered" } },
        { VersionType.Version, new[] { "version", "alternative version", "extended version", "short version", "single version" } },
        { VersionType.Live, new[] { "live", "live version", "live at", "live from", "acoustic live" } },
        { VersionType.Acoustic, new[] { "acoustic", "acoustic version", "unplugged" } },
        { VersionType.Demo, new[] { "demo", "demo version", "demo recording" } }
    };

    public static VersionMatchResult? MatchVersion(Track track, MusicEnhancerOptions options)
    {
        if (track == null)
        {
            throw new ArgumentNullException(nameof(track));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrEmpty(track.Title))
        {
            return null;
        }

        var title = track.Title.ToLowerInvariant();

        foreach (var kvp in VersionPatterns)
        {
            foreach (var pattern in kvp.Value)
            {
                if (title.Contains(pattern))
                {
                    return new VersionMatchResult(track.Title, null, kvp.Key, 0.95, $"Detected {kvp.Key} version based on title pattern");
                }
            }
        }

        return null;
    }
}
