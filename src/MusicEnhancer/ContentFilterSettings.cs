using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Plugins;

/// <summary>
/// Lidarr provider settings for the Music Enhancer connection.
/// </summary>
public class MusicEnhancerSettings : IProviderConfig
{
    private const string DefaultReleaseTypesString = "Album, EP";
    private static readonly MusicEnhancerSettingsValidator Validator = new MusicEnhancerSettingsValidator();

    public MusicEnhancerSettings()
    {
        VersionFilterAction = VersionFilterAction.FlagOnly;
        ReleaseTypesToFilter = DefaultReleaseTypesString;
        DurationToleranceMs = 3000;
        EnableRemixFiltering = true;
        EnableVersionFiltering = true;
        EnableLiveVersionFiltering = true;
        EnableAcousticVersionFiltering = true;
        EnableEditVersionFiltering = true;
        EnableDemoVersionFiltering = true;
        EnableCommentaryFiltering = true;
        MinimumTrackDurationMs = 30000; // 30 seconds minimum
        MaximumTrackDurationMs = 600000; // 10 minutes maximum
        DeleteFilesAfterUnmonitor = false;
        EnableExplicitTagging = true;
        EnableMetadataWriting = true;
        KeepCleanVersions = true;
        ScanLibraryOnStartup = false;
    }

    [FieldDefinition(0, Label = "Version filter action", Type = FieldType.Select, SelectOptions = typeof(VersionFilterAction), HelpText = "Flag logs matches; Unmonitor applies only if all known tracks match. Mixed albums stay monitored. Defaults to flag for review.")]
    public VersionFilterAction VersionFilterAction { get; set; }

    [FieldDefinition(1, Label = "Release types to filter", Type = FieldType.Textbox, HelpText = "Comma-separated release types to apply filtering to. Defaults to Album, EP.")]
    public string ReleaseTypesToFilter { get; set; }

    [FieldDefinition(2, Label = "Legacy duration tolerance (unused)", Type = FieldType.Number, Advanced = true, HelpText = "Retained for compatibility; the version matcher does not use this setting.")]
    public int DurationToleranceMs { get; set; }

    [FieldDefinition(3, Label = "Enable remix filtering", Type = FieldType.Checkbox, HelpText = "Filter titles marked Remix, Bootleg, Rework, etc. On by default.")]
    public bool EnableRemixFiltering { get; set; }

    [FieldDefinition(4, Label = "Enable version filtering", Type = FieldType.Checkbox, HelpText = "Filter version markers (Version, Alternative Version, Extended Version, etc.). On by default.")]
    public bool EnableVersionFiltering { get; set; }

    [FieldDefinition(5, Label = "Enable live version filtering", Type = FieldType.Checkbox, HelpText = "Filter live versions (Live, Live Version, Live at, etc.). On by default.")]
    public bool EnableLiveVersionFiltering { get; set; }

    [FieldDefinition(6, Label = "Enable acoustic filtering", Type = FieldType.Checkbox, HelpText = "Filter titles marked Acoustic or Unplugged. On by default.")]
    public bool EnableAcousticVersionFiltering { get; set; }

    [FieldDefinition(7, Label = "Enable edit filtering", Type = FieldType.Checkbox, HelpText = "Filter edit versions (Edit, Radio Edit, Album Edit, etc.). On by default.")]
    public bool EnableEditVersionFiltering { get; set; }

    [FieldDefinition(8, Label = "Enable demo filtering", Type = FieldType.Checkbox, HelpText = "Filter demo versions (Demo, Demo Version, Demo Recording, etc.). On by default.")]
    public bool EnableDemoVersionFiltering { get; set; }

    [FieldDefinition(9, Label = "Minimum track duration (ms)", Type = FieldType.Number, HelpText = "Ignore short version tracks, except commentary. Defaults to 30000 ms (30 seconds).")]
    public int MinimumTrackDurationMs { get; set; }

    [FieldDefinition(10, Label = "Maximum track duration (ms)", Type = FieldType.Number, HelpText = "Ignore long version tracks, except commentary. Defaults to 600000 ms (10 minutes).")]
    public int MaximumTrackDurationMs { get; set; }

    [FieldDefinition(11, Label = "Delete matched track files", Type = FieldType.Checkbox, HelpText = "When action is Unmonitor, also delete individually matched, unshared track files. Off by default for new connections. Existing settings are preserved.")]
    public bool DeleteFilesAfterUnmonitor { get; set; }

    [FieldDefinition(12, Label = "Enable explicit tagging", Type = FieldType.Checkbox, HelpText = "Tag tracks with known explicit or clean ratings. Existing advisory tags are preserved.")]
    public bool EnableExplicitTagging { get; set; }

    [FieldDefinition(13, Label = "Enable metadata writing", Type = FieldType.Checkbox, HelpText = "Allow metadata and lyric file writes when metadata processing is also enabled. On by default.")]
    public bool EnableMetadataWriting { get; set; }

    [FieldDefinition(14, Label = "Last.fm API Key", Type = FieldType.Textbox, Privacy = PrivacyLevel.ApiKey, HelpText = "Optional Last.fm key for mood lookup. No key is required for the other lookups.")]
    public string? LastFmApiKey { get; set; }

    [FieldDefinition(15, Label = "Legacy Deezer API Key (unused)", Type = FieldType.Textbox, Privacy = PrivacyLevel.ApiKey, Advanced = true, HelpText = "Retained to preserve existing settings. Deezer lookups do not need this key.")]
    public string? DeezerApiKey { get; set; }

    [FieldDefinition(16, Label = "Legacy Discogs API Key (unused)", Type = FieldType.Textbox, Privacy = PrivacyLevel.ApiKey, Advanced = true, HelpText = "Retained to preserve existing settings; unused.")]
    public string? DiscogsApiKey { get; set; }

    [FieldDefinition(17, Label = "Legacy Discogs API Secret (unused)", Type = FieldType.Password, Privacy = PrivacyLevel.Password, Advanced = true, HelpText = "Retained to preserve existing settings; unused.")]
    public string? DiscogsApiSecret { get; set; }

    [FieldDefinition(18, Label = "Legacy AcoustID API Key (unused)", Type = FieldType.Textbox, Privacy = PrivacyLevel.ApiKey, Advanced = true, HelpText = "Retained to preserve existing settings; unused.")]
    public string? AcoustIdApiKey { get; set; }

    [FieldDefinition(19, Label = "Keep clean versions", Type = FieldType.Checkbox, HelpText = "Preserve clean title markers or advisory tags when filtering edits and versions. Skips files whose advisory cannot be inspected. On by default.")]
    public bool KeepCleanVersions { get; set; }

    [FieldDefinition(20, Label = "Enable commentary filtering", Type = FieldType.Checkbox, HelpText = "Filter commentary tracks (Commentary, Album Commentary, Track Commentary, etc.). On by default.")]
    public bool EnableCommentaryFiltering { get; set; }

    [FieldDefinition(21, Label = "Scan library on startup", Type = FieldType.Checkbox, HelpText = "Run filtering and enabled metadata tagging over existing monitored albums on startup. May take a long time. Off by default.")]
    public bool ScanLibraryOnStartup { get; set; }

    [FieldDefinition(22, Label = "Look up explicit status online", Type = FieldType.Checkbox, HelpText = "Use MusicBrainz recording IDs and Deezer to look up explicit status without an API key. Sends recording identifiers to these services. Off by default.")]
    public bool EnableOnlineExplicitLookup { get; set; }

    [FieldDefinition(23, Label = "Enable metadata processing", Type = FieldType.Checkbox, HelpText = "Allow Music Enhancer to change audio file tags. Off by default, including for existing connections. Back up your files before enabling.")]
    public bool EnableMetadataProcessing { get; set; }

    [FieldDefinition(24, Label = "Fill missing library tags", Type = FieldType.Checkbox, HelpText = "Fill missing standard tags from Lidarr's track, release and artist metadata. Existing values are preserved. Off by default.")]
    public bool EnableLibraryTagging { get; set; }

    [FieldDefinition(25, Label = "Look up lyrics online", Type = FieldType.Checkbox, HelpText = "Look up matching lyrics on LRCLIB and add a missing .lrc sidecar. Sends artist, album and track details to LRCLIB. Off by default.")]
    public bool EnableLyricsLookup { get; set; }

    [FieldDefinition(26, Label = "Look up BPM online", Type = FieldType.Checkbox, HelpText = "Fill missing BPM using an ISRC from the audio file or MusicBrainz, and Deezer. Requires Fill missing library tags. Off by default.")]
    public bool EnableBpmLookup { get; set; }

    [FieldDefinition(27, Label = "Look up composers online", Type = FieldType.Checkbox, HelpText = "Use MusicBrainz work relationships to fill missing composer credits. Requires Fill missing library tags. Off by default.")]
    public bool EnableComposerLookup { get; set; }

    [FieldDefinition(28, Label = "Look up mood online", Type = FieldType.Checkbox, HelpText = "Fill a missing mood from recognized Last.fm community mood tags using the recording ID. Requires a Last.fm API key and Fill missing library tags. Off by default.")]
    public bool EnableMoodLookup { get; set; }

    [FieldDefinition(29, Label = "Look up recording metadata online", Type = FieldType.Checkbox, HelpText = "Use MusicBrainz to fill missing recording credits, work, community track tags and recognizable moods. Requires Fill missing library tags. Off by default.")]
    public bool EnableRecordingMetadataLookup { get; set; }

    /// <summary>
    /// Converts settings to a validated MusicEnhancerOptions snapshot.
    /// </summary>
    public MusicEnhancerOptions ToFilterOptions()
    {
        var releaseTypes = ParseReleaseTypes(ReleaseTypesToFilter);
        return new MusicEnhancerOptions(
            releaseTypes,
            VersionFilterAction,
            EnableRemixFiltering,
            EnableVersionFiltering,
            EnableLiveVersionFiltering,
            EnableAcousticVersionFiltering,
            EnableEditVersionFiltering,
            EnableDemoVersionFiltering,
            EnableCommentaryFiltering,
            MinimumTrackDurationMs,
            MaximumTrackDurationMs,
            DeleteFilesAfterUnmonitor,
            EnableExplicitTagging,
            EnableMetadataWriting,
            KeepCleanVersions,
            LastFmApiKey,
            EnableOnlineExplicitLookup,
            EnableMetadataProcessing,
            EnableLibraryTagging,
            EnableLyricsLookup,
            EnableBpmLookup,
            EnableComposerLookup,
            EnableMoodLookup,
            EnableRecordingMetadataLookup);
    }

    private static HashSet<string> ParseReleaseTypes(string? releaseTypesString)
    {
        if (string.IsNullOrWhiteSpace(releaseTypesString))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Album", "EP" };
        }

        var types = releaseTypesString.Split(',')
            .Select(t => NormalizeReleaseType(t.Trim()))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        return types.Count > 0
            ? new HashSet<string>(types, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Album", "EP" };
    }

    private static string NormalizeReleaseType(string releaseType)
    {
        if (string.IsNullOrWhiteSpace(releaseType))
        {
            return string.Empty;
        }

        var normalized = releaseType.ToLowerInvariant();

        return normalized switch
        {
            "album" => "Album",
            "ep" => "EP",
            "single" => "Single",
            "compilation" => "Compilation",
            _ => char.ToUpperInvariant(releaseType[0]) + releaseType[1..].ToLowerInvariant()
        };
    }

    public NzbDroneValidationResult Validate()
    {
        return new NzbDroneValidationResult(Validator.Validate(this));
    }
}

public class MusicEnhancerSettingsValidator : AbstractValidator<MusicEnhancerSettings>
{
    public MusicEnhancerSettingsValidator()
    {
        RuleFor(c => c.VersionFilterAction).IsInEnum()
            .WithMessage("Select a valid version filter action");
        RuleFor(c => c.MinimumTrackDurationMs).GreaterThanOrEqualTo(0)
            .WithMessage("Minimum track duration must be non-negative");
        RuleFor(c => c.MaximumTrackDurationMs).GreaterThan(c => c.MinimumTrackDurationMs)
            .WithMessage("Maximum track duration must be greater than minimum track duration");
    }
}
