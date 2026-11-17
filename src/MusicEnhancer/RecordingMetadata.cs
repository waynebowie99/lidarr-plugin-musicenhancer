namespace NzbDrone.Core.Plugins;

/// <summary>Optional recording-level information; absent fields remain unknown.</summary>
public sealed class RecordingMetadata
{
    public string[]? Artists { get; set; }
    public string[]? ArtistSortNames { get; set; }
    public string? Work { get; set; }
    public string[]? CommunityTags { get; set; }
    public string[]? Moods { get; set; }
    public string[]? Isrcs { get; set; }
    public string[]? Composers { get; set; }
    public AdvisoryRating? AdvisoryRating { get; set; }
}
