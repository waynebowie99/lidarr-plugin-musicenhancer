using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Plugins;

/// <summary>
/// Options for content filtering behavior.
/// </summary>
public class MusicEnhancerOptions
{
    public HashSet<string> ReleaseTypesToFilter { get; }
    public VersionFilterAction VersionFilterAction { get; }
    public int DurationToleranceMs { get; }
    public bool EnableRemixFiltering { get; }
    public bool EnableVersionFiltering { get; }
    public bool EnableLiveVersionFiltering { get; }
    public bool EnableAcousticVersionFiltering { get; }
    public bool EnableEditVersionFiltering { get; }
    public bool EnableDemoVersionFiltering { get; }
    public int MinimumTrackDurationMs { get; }
    public int MaximumTrackDurationMs { get; }
    public bool DeleteFilesAfterUnmonitor { get; }
    public bool EnableExplicitTagging { get; }
    public bool EnableMetadataWriting { get; }

    public MusicEnhancerOptions(
        HashSet<string> releaseTypesToFilter,
        VersionFilterAction versionFilterAction,
        int durationToleranceMs,
        bool enableRemixFiltering = true,
        bool enableVersionFiltering = true,
        bool enableLiveVersionFiltering = true,
        bool enableAcousticVersionFiltering = true,
        bool enableEditVersionFiltering = true,
        bool enableDemoVersionFiltering = true,
        int minimumTrackDurationMs = 30000,
        int maximumTrackDurationMs = 600000,
        bool deleteFilesAfterUnmonitor = true,
        bool enableExplicitTagging = true,
        bool enableMetadataWriting = true)
    {
        ReleaseTypesToFilter = releaseTypesToFilter ?? throw new ArgumentNullException(nameof(releaseTypesToFilter));
        VersionFilterAction = versionFilterAction;
        DurationToleranceMs = durationToleranceMs;
        EnableRemixFiltering = enableRemixFiltering;
        EnableVersionFiltering = enableVersionFiltering;
        EnableLiveVersionFiltering = enableLiveVersionFiltering;
        EnableAcousticVersionFiltering = enableAcousticVersionFiltering;
        EnableEditVersionFiltering = enableEditVersionFiltering;
        EnableDemoVersionFiltering = enableDemoVersionFiltering;
        MinimumTrackDurationMs = minimumTrackDurationMs;
        MaximumTrackDurationMs = maximumTrackDurationMs;
        DeleteFilesAfterUnmonitor = deleteFilesAfterUnmonitor;
        EnableExplicitTagging = enableExplicitTagging;
        EnableMetadataWriting = enableMetadataWriting;
    }

    public bool ShouldFilterReleaseType(string releaseType)
    {
        return ReleaseTypesToFilter.Contains(releaseType);
    }
}

/// <summary>
/// Represents a version match result for a track.
/// </summary>
public class VersionMatchResult
{
    public Track Track { get; }
    public Track? MatchedTrack { get; }
    public VersionType VersionType { get; }
    public double Confidence { get; }
    public string Reason { get; }

    public VersionMatchResult(Track track, Track? matchedTrack, VersionType versionType, double confidence, string reason)
    {
        Track = track;
        MatchedTrack = matchedTrack;
        VersionType = versionType;
        Confidence = confidence;
        Reason = reason;
    }
}

/// <summary>
/// Represents the type of version detected.
/// </summary>
public enum VersionType
{
    None,
    Remix,
    Edit,
    Version,
    Live,
    Acoustic,
    Demo
}

/// <summary>
/// Contains filtering results for a release.
/// </summary>
public class FilterResult
{
    public int CandidatesChecked { get; }
    public int AlbumsUnmonitored { get; }
    public int TracksUnmonitored { get; }
    public int Skipped { get; }
    public int ReviewNeeded { get; }
    public int UnmonitorFailures { get; }
    public int DeleteFailures { get; }

    public FilterResult(int candidatesChecked, int albumsUnmonitored, int tracksUnmonitored, int skipped, int reviewNeeded, int unmonitorFailures, int deleteFailures)
    {
        CandidatesChecked = candidatesChecked;
        AlbumsUnmonitored = albumsUnmonitored;
        TracksUnmonitored = tracksUnmonitored;
        Skipped = skipped;
        ReviewNeeded = reviewNeeded;
        UnmonitorFailures = unmonitorFailures;
        DeleteFailures = deleteFailures;
    }

    public static FilterResult Empty => new FilterResult(0, 0, 0, 0, 0, 0, 0);

    public static FilterResult operator +(FilterResult left, FilterResult right)
    {
        return new FilterResult(
            left.CandidatesChecked + right.CandidatesChecked,
            left.AlbumsUnmonitored + right.AlbumsUnmonitored,
            left.TracksUnmonitored + right.TracksUnmonitored,
            left.Skipped + right.Skipped,
            left.ReviewNeeded + right.ReviewNeeded,
            left.UnmonitorFailures + right.UnmonitorFailures,
            left.DeleteFailures + right.DeleteFailures);
    }
}
