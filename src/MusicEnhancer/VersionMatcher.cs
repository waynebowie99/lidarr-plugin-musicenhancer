using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

/// <summary>
/// Service for matching tracks to version types.
/// </summary>
public static class VersionMatcher
{
    private static readonly Dictionary<VersionType, string[]> VersionPatterns = new Dictionary<VersionType, string[]>
    {
        { VersionType.Remix, new[] { "remix", "bootleg", "reinterpretation", "rework", "flip", "mashup", "remake" } },
        { VersionType.Edit, new[] { "edit", "radio edit", "album edit", "remaster", "remastered" } },
        { VersionType.Version, new[] { "version", "alternative version", "extended version", "short version", "single version" } },
        { VersionType.Live, new[] { "live", "live version", "live at", "live from", "acoustic live" } },
        { VersionType.Acoustic, new[] { "acoustic", "acoustic version", "unplugged" } },
        { VersionType.Demo, new[] { "demo", "demo version", "demo recording" } }
    };

    // Clean edits are a separate category: they are explicit-free versions of a
    // song, not alternate edits. Match common forms like "(Clean)",
    // "[Clean Version]", "Clean Edit" and "- Clean".
    private static readonly Regex[] CleanPatterns =
    {
        new(@"\(\s*clean(?:\s+(?:version|edit|mix|album|radio|single))?\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\[\s*clean(?:\s+(?:version|edit|mix|album|radio|single))?\s*\]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bclean\s+(?:version|edit|mix|album|radio|single)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\s-\s*clean(?:\s+(?:version|edit|mix|album|radio|single))?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
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

        // Check clean markers first so "Clean Edit" is not classified as a
        // regular edit and "Clean Version" is not classified as a version.
        if (CleanPatterns.Any(pattern => pattern.IsMatch(title)))
        {
            return new VersionMatchResult(track.Title, default, VersionType.Clean, 0.95, "Detected clean version marker");
        }

        foreach (var kvp in VersionPatterns)
        {
            foreach (var pattern in kvp.Value)
            {
                if (title.Contains(pattern))
                {
                    return new VersionMatchResult(track.Title, default, kvp.Key, 0.95, $"Detected {kvp.Key} version based on title pattern");
                }
            }
        }

        return null;
    }
}
