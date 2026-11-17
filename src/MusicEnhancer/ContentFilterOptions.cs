using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Plugins;

/// <summary>
/// Represents a version match result for a track.
/// </summary>
public class VersionMatchResult
{
    public VersionType VersionType { get; }
    public string Reason { get; }

    public VersionMatchResult(VersionType versionType, string reason)
    {
        VersionType = versionType;
        Reason = reason;
    }
}

/// <summary>
/// Represents the type of version detected.
/// </summary>
public enum VersionType
{
    Remix,
    Edit,
    Version,
    Live,
    Acoustic,
    Demo,
    Clean,
    Commentary
}

/// <summary>
/// Options for content filtering.
/// </summary>
public class MusicEnhancerOptions
{
    public HashSet<string> ReleaseTypesToFilter { get; }
    public VersionFilterAction VersionFilterAction { get; }
    public bool EnableRemixFiltering { get; }
    public bool EnableVersionFiltering { get; }
    public bool EnableLiveVersionFiltering { get; }
    public bool EnableAcousticVersionFiltering { get; }
    public bool EnableEditVersionFiltering { get; }
    public bool EnableDemoVersionFiltering { get; }
    public bool EnableCommentaryFiltering { get; }
    public int MinimumTrackDurationMs { get; }
    public int MaximumTrackDurationMs { get; }
    public bool DeleteFilesAfterUnmonitor { get; }
    public bool EnableExplicitTagging { get; }
    public bool EnableMetadataWriting { get; }
    public bool EnableOnlineExplicitLookup { get; }
    public bool EnableMetadataProcessing { get; }
    public bool EnableLibraryTagging { get; }
    public bool EnableLyricsLookup { get; }
    public bool EnableBpmLookup { get; }
    public bool EnableComposerLookup { get; }
    public bool EnableMoodLookup { get; }
    public bool EnableRecordingMetadataLookup { get; }
    public bool KeepCleanVersions { get; }
    public string? LastFmApiKey { get; }

    public MusicEnhancerOptions(
        HashSet<string> releaseTypesToFilter,
        VersionFilterAction versionFilterAction,
        bool enableRemixFiltering,
        bool enableVersionFiltering,
        bool enableLiveVersionFiltering,
        bool enableAcousticVersionFiltering,
        bool enableEditVersionFiltering,
        bool enableDemoVersionFiltering,
        bool enableCommentaryFiltering,
        int minimumTrackDurationMs,
        int maximumTrackDurationMs,
        bool deleteFilesAfterUnmonitor,
        bool enableExplicitTagging,
        bool enableMetadataWriting,
        bool keepCleanVersions,
        string? lastFmApiKey,
        bool enableOnlineExplicitLookup = false,
        bool enableMetadataProcessing = false,
        bool enableLibraryTagging = false,
        bool enableLyricsLookup = false,
        bool enableBpmLookup = false,
        bool enableComposerLookup = false,
        bool enableMoodLookup = false,
        bool enableRecordingMetadataLookup = false)
    {
        ReleaseTypesToFilter = releaseTypesToFilter ?? new HashSet<string>();
        VersionFilterAction = versionFilterAction;
        EnableRemixFiltering = enableRemixFiltering;
        EnableVersionFiltering = enableVersionFiltering;
        EnableLiveVersionFiltering = enableLiveVersionFiltering;
        EnableAcousticVersionFiltering = enableAcousticVersionFiltering;
        EnableEditVersionFiltering = enableEditVersionFiltering;
        EnableDemoVersionFiltering = enableDemoVersionFiltering;
        EnableCommentaryFiltering = enableCommentaryFiltering;
        MinimumTrackDurationMs = minimumTrackDurationMs;
        MaximumTrackDurationMs = maximumTrackDurationMs;
        DeleteFilesAfterUnmonitor = deleteFilesAfterUnmonitor;
        EnableExplicitTagging = enableExplicitTagging;
        EnableMetadataWriting = enableMetadataWriting;
        EnableOnlineExplicitLookup = enableOnlineExplicitLookup;
        EnableMetadataProcessing = enableMetadataProcessing;
        EnableLibraryTagging = enableLibraryTagging;
        EnableLyricsLookup = enableLyricsLookup;
        EnableBpmLookup = enableBpmLookup;
        EnableComposerLookup = enableComposerLookup;
        EnableMoodLookup = enableMoodLookup;
        EnableRecordingMetadataLookup = enableRecordingMetadataLookup;
        KeepCleanVersions = keepCleanVersions;
        LastFmApiKey = lastFmApiKey;
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
