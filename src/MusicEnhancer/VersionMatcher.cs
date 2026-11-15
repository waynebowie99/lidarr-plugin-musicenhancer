using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

/// <summary>Conservative title-marker matching; ordinary words inside song names are not versions.</summary>
public static class VersionMatcher
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;
    private static readonly Regex Commentary = new(@"\bcommentary\s*$", Options);
    private static readonly Regex Original = new(@"^\s*original\s+(?:version|mix)\s*$", Options);
    private static readonly Regex Qualifiers = new(@"\((?<marker>[^()]*)\)|\[(?<marker>[^\[\]]*)\]|\s[-–—]\s*(?<marker>[^\[\]()]*)$", Options);
    private static readonly Regex Suffix = new(@"\s+(?<marker>(?:radio|album|single|extended|club|instrumental)\s+edit|(?:extended|alternative|alternate|short|single|album|live|acoustic|demo)\s+version|extended\s+mix|live\s+(?:at|from)\s+.+|remix|bootleg|mashup|rework|unplugged)\s*$", Options);

    private static readonly Regex[] CleanPatterns =
    {
        new(@"\(\s*clean(?:\s+(?:version|edit|mix|album|radio|single))?\s*\)", Options),
        new(@"\[\s*clean(?:\s+(?:version|edit|mix|album|radio|single))?\s*\]", Options),
        new(@"\bclean\s+(?:version|edit|mix|album|radio|single)\b", Options),
        new(@"\s[-–—]\s*clean(?:\s+(?:version|edit|mix|album|radio|single))?\s*$", Options)
    };

    private static readonly (VersionType Type, Regex Pattern)[] VersionPatterns =
    {
        (VersionType.Remix, new Regex(@"\b(?:remix|bootleg|reinterpretation|rework|flip|mashup|remake)\b", Options)),
        (VersionType.Edit, new Regex(@"\bedit\b", Options)),
        (VersionType.Live, new Regex(@"^live(?:\s+(?:at|from|in|on|version|recording|performance)\b.*)?$", Options)),
        (VersionType.Acoustic, new Regex(@"^(?:acoustic|unplugged)(?:\s+(?:version|mix|performance|session|live)\b.*)?$", Options)),
        (VersionType.Demo, new Regex(@"^demo(?:\s+(?:version|recording|mix)\b.*)?$", Options)),
        (VersionType.Version, new Regex(@"\bversion\b|\bextended\s+mix\b", Options))
    };

    public static VersionMatchResult? MatchVersion(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (string.IsNullOrWhiteSpace(track.Title)) return null;

        var title = track.Title.Trim();
        var markers = new List<string>();
        foreach (Match match in Qualifiers.Matches(title))
        {
            markers.Add(match.Groups["marker"].Value.Trim());
        }
        var suffix = Suffix.Match(title);
        if (suffix.Success) markers.Add(suffix.Groups["marker"].Value.Trim());

        if (Commentary.IsMatch(title) || markers.Any(marker => Commentary.IsMatch(marker)))
        {
            return new VersionMatchResult(VersionType.Commentary, "Detected commentary marker");
        }
        if (CleanPatterns.Any(pattern => pattern.IsMatch(title)))
        {
            return new VersionMatchResult(VersionType.Clean, "Detected clean version marker");
        }

        foreach (var marker in markers.Where(marker => !Original.IsMatch(marker)))
        {
            foreach (var (type, pattern) in VersionPatterns)
            {
                if (pattern.IsMatch(marker)) return new VersionMatchResult(type, $"Detected {type} title marker");
            }
        }
        return null;
    }
}
