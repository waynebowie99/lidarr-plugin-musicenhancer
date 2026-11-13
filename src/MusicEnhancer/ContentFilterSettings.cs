using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Plugins;

public enum VersionFilterAction
{
    [FieldOption(Label = "Flag for review", Hint = "Flag versions for manual review. Never auto-unmonitor.")]
    FlagOnly = 0,

    [FieldOption(Label = "Unmonitor", Hint = "Automatically unmonitor identified versions.")]
    Unmonitor = 1,

    [FieldOption(Label = "Skip", Hint = "Ignore versions and leave them monitored.")]
    Skip = 2
}

public enum ReleaseTypeFilter
{
    [FieldOption(Label = "Albums", Hint = "Filter remixes and versions on albums")]
    Album = 0,

    [FieldOption(Label = "EPs", Hint = "Filter remixes and versions on EPs")]
    EP = 1,

    [FieldOption(Label = "Singles", Hint = "Filter remixes and versions on singles")]
    Single = 2,

    [FieldOption(Label = "Compilations", Hint = "Filter remixes and versions on compilations")]
    Compilation = 3
}

/// <summary>
/// Lidarr provider settings for the Music Enhancer connection.
/// </summary>
public class MusicEnhancerSettings : IProviderConfig
{
    private static readonly ReleaseTypeFilter[] DefaultReleaseTypes = { ReleaseTypeFilter.Album, ReleaseTypeFilter.EP };
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
        MinimumTrackDurationMs = 30000; // 30 seconds minimum
        MaximumTrackDurationMs = 600000; // 10 minutes maximum
        DeleteFilesAfterUnmonitor = true; // Default to delete files
    }

    [FieldDefinition(0, Label = "Version filter action", Type = FieldType.Select, SelectOptions = typeof(VersionFilterAction), HelpText = "Action to take when a version is detected. Defaults to flag for review.")]
    public VersionFilterAction VersionFilterAction { get; set; }

    [FieldDefinition(1, Label = "Release types to filter", Type = FieldType.Textbox, HelpText = "Comma-separated release types to apply filtering to. Defaults to Album, EP.")]
    public string ReleaseTypesToFilter { get; set; }

    [FieldDefinition(2, Label = "Duration tolerance (ms)", Type = FieldType.Number, HelpText = "Maximum duration difference for title + duration matching. Defaults to 3000 ms.")]
    public int DurationToleranceMs { get; set; }

    [FieldDefinition(3, Label = "Enable remix filtering", Type = FieldType.Checkbox, HelpText = "Filter remixes (Remix, Edit, Bootleg, Reinterpretation, etc.). On by default.")]
    public bool EnableRemixFiltering { get; set; }

    [FieldDefinition(4, Label = "Enable version filtering", Type = FieldType.Checkbox, HelpText = "Filter version markers (Version, Alternative Version, Extended Version, etc.). On by default.")]
    public bool EnableVersionFiltering { get; set; }

    [FieldDefinition(5, Label = "Enable live version filtering", Type = FieldType.Checkbox, HelpText = "Filter live versions (Live, Live Version, Live at, etc.). On by default.")]
    public bool EnableLiveVersionFiltering { get; set; }

    [FieldDefinition(6, Label = "Enable acoustic filtering", Type = FieldType.Checkbox, HelpText = "Filter acoustic versions (Acoustic, Acoustic Version, Acoustic Version, etc.). On by default.")]
    public bool EnableAcousticVersionFiltering { get; set; }

    [FieldDefinition(7, Label = "Enable edit filtering", Type = FieldType.Checkbox, HelpText = "Filter edit versions (Edit, Radio Edit, Album Edit, etc.). On by default.")]
    public bool EnableEditVersionFiltering { get; set; }

    [FieldDefinition(8, Label = "Enable demo filtering", Type = FieldType.Checkbox, HelpText = "Filter demo versions (Demo, Demo Version, Demo Recording, etc.). On by default.")]
    public bool EnableDemoVersionFiltering { get; set; }

    [FieldDefinition(9, Label = "Minimum track duration (ms)", Type = FieldType.Number, HelpText = "Minimum track duration in milliseconds. Tracks shorter than this are ignored. Defaults to 30000 ms (30 seconds).")]
    public int MinimumTrackDurationMs { get; set; }

    [FieldDefinition(10, Label = "Maximum track duration (ms)", Type = FieldType.Number, HelpText = "Maximum track duration in milliseconds. Tracks longer than this are ignored. Defaults to 600000 ms (10 minutes).")]
    public int MaximumTrackDurationMs { get; set; }

    [FieldDefinition(11, Label = "Delete files after unmonitor", Type = FieldType.Checkbox, HelpText = "Delete media files after unmonitoring a version. Defaults to true (delete files).")]
    public bool DeleteFilesAfterUnmonitor { get; set; }

    /// <summary>
    /// Converts settings to a validated MusicEnhancerOptions snapshot.
    /// </summary>
    public MusicEnhancerOptions ToFilterOptions()
    {
        var releaseTypes = ParseReleaseTypes(ReleaseTypesToFilter);
        return new MusicEnhancerOptions(
            releaseTypes,
            VersionFilterAction,
            DurationToleranceMs,
            EnableRemixFiltering,
            EnableVersionFiltering,
            EnableLiveVersionFiltering,
            EnableAcousticVersionFiltering,
            EnableEditVersionFiltering,
            EnableDemoVersionFiltering,
            MinimumTrackDurationMs,
            MaximumTrackDurationMs,
            DeleteFilesAfterUnmonitor,
            EnableExplicitTagging,
            EnableMetadataWriting,
            LastFmApiKey,
            DeezerApiKey,
            DiscogsApiKey,
            DiscogsApiSecret,
            AcoustIdApiKey);
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

/// <summary>
/// Metadata enhancement settings for explicit tag support.
/// </summary>
public class MetadataEnhancerSettings
{
    private static readonly MetadataEnhancerSettingsValidator Validator = new MetadataEnhancerSettingsValidator();

    public MetadataEnhancerSettings()
    {
        EnableExplicitTagging = true;
        EnableMetadataWriting = true;
    }

    [FieldDefinition(12, Label = "Enable explicit tagging", Type = FieldType.Checkbox, HelpText = "Automatically detect and tag explicit content in track metadata. Required for Symfonium/Jellyfin compatibility. On by default.")]
    public bool EnableExplicitTagging { get; set; }

    [FieldDefinition(13, Label = "Enable metadata writing", Type = FieldType.Checkbox, HelpText = "Write metadata tags to files. Required for explicit tagging to work. On by default.")]
    public bool EnableMetadataWriting { get; set; }

    [FieldDefinition(14, Label = "Last.fm API Key", Type = FieldType.Textbox, HelpText = "API key for Last.fm metadata enrichment (play counts, ratings, tags, genres). Optional but recommended.")]
    public string LastFmApiKey { get; set; }

    [FieldDefinition(15, Label = "Deezer API Key", Type = FieldType.Textbox, HelpText = "API key for Deezer metadata enrichment (duration, preview, album info). Optional but recommended.")]
    public string DeezerApiKey { get; set; }

    [FieldDefinition(16, Label = "Discogs API Key", Type = FieldType.Textbox, HelpText = "API key for Discogs metadata enrichment (release date, label, catalog number). Optional but recommended.")]
    public string DiscogsApiKey { get; set; }

    [FieldDefinition(17, Label = "Discogs API Secret", Type = FieldType.Password, HelpText = "API secret for Discogs authentication. Optional but recommended.")]
    public string DiscogsApiSecret { get; set; }

    [FieldDefinition(18, Label = "AcoustID API Key", Type = FieldType.Textbox, HelpText = "API key for AcoustID audio fingerprinting (MusicBrainz Recording ID, duration accuracy). Optional but recommended.")]
    public string AcoustIdApiKey { get; set; }

    public NzbDroneValidationResult Validate()
    {
        return new NzbDroneValidationResult(Validator.Validate(this));
    }
}

public class MetadataEnhancerSettingsValidator : AbstractValidator<MetadataEnhancerSettings>
{
    public MetadataEnhancerSettingsValidator()
    {
        // Validation rules for metadata enhancement settings
    }
}

public class MusicEnhancerSettingsValidator : AbstractValidator<MusicEnhancerSettings>
{
    public ContentFilterSettingsValidator()
    {
        RuleFor(c => c.DurationToleranceMs).GreaterThan(0)
            .WithMessage("Duration tolerance must be greater than 0");
        RuleFor(c => c.MinimumTrackDurationMs).GreaterThanOrEqualTo(0)
            .WithMessage("Minimum track duration must be non-negative");
        RuleFor(c => c.MaximumTrackDurationMs).GreaterThan(c => c.MinimumTrackDurationMs)
            .WithMessage("Maximum track duration must be greater than minimum track duration");
    }
}
