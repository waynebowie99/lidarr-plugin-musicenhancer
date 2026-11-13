using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Plugins;

/// <summary>
/// Represents a version match result for a track.
/// </summary>
public class VersionMatchResult
{
    public string TrackTitle { get; set; }
    public string MatchedTrackTitle { get; }
    public VersionType VersionType { get; }
    public double Confidence { get; }
    public string Reason { get; }

    public VersionMatchResult(string trackTitle, string matchedTrackTitle, VersionType versionType, double confidence, string reason)
    {
        TrackTitle = trackTitle;
        MatchedTrackTitle = matchedTrackTitle;
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
/// Options for content filtering.
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
    public string LastFmApiKey { get; }
    public string DeezerApiKey { get; }
    public string DiscogsApiKey { get; }
    public string DiscogsApiSecret { get; }
    public string AcoustIdApiKey { get; }

    public MusicEnhancerOptions(
        HashSet<string> releaseTypesToFilter,
        VersionFilterAction versionFilterAction,
        int durationToleranceMs,
        bool enableRemixFiltering,
        bool enableVersionFiltering,
        bool enableLiveVersionFiltering,
        bool enableAcousticVersionFiltering,
        bool enableEditVersionFiltering,
        bool enableDemoVersionFiltering,
        int minimumTrackDurationMs,
        int maximumTrackDurationMs,
        bool deleteFilesAfterUnmonitor,
        bool enableExplicitTagging,
        bool enableMetadataWriting,
        string lastFmApiKey,
        string deezerApiKey,
        string discogsApiKey,
        string discogsApiSecret,
        string acoustIdApiKey)
    {
        ReleaseTypesToFilter = releaseTypesToFilter ?? new HashSet<string>();
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
        LastFmApiKey = lastFmApiKey;
        DeezerApiKey = deezerApiKey;
        DiscogsApiKey = discogsApiKey;
        DiscogsApiSecret = discogsApiSecret;
        AcoustIdApiKey = acoustIdApiKey;
    }
}

/// <summary>
/// Action to take when a version is detected.
/// </summary>
public enum VersionFilterAction
{
    FlagOnly = 0,
    Unmonitor = 1,
    Skip = 2
}
