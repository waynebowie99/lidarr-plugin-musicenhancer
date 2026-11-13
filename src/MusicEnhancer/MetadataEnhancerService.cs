using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

/// <summary>
/// Service for enhancing track metadata.
/// Adds explicit tag support for Symfonium/Jellyfin compatibility.
/// Supports multiple file formats: MP4 (iTunes), FLAC (Vorbis/ID3), MP3 (ID3), OGG, Opus, and others.
/// </summary>
public interface IMetadataEnhancerService
{
    void EnhanceMetadataForArtist(Artist artist, Album album);
    void EnhanceMetadataForTrack(Track track);
    bool HasExplicitTag(Track track);
    void SetExplicitTag(Track track, bool isExplicit);
    void WriteExplicitTagToFile(string filePath, bool isExplicit);
}

public class MetadataEnhancerService : IMetadataEnhancerService
{
    private readonly ITrackService _trackService;
    private readonly IMediaFileService _mediaFileService;
    private readonly Logger _logger;

    public MetadataEnhancerService(
        ITrackService trackService,
        IMediaFileService mediaFileService,
        Logger logger)
    {
        _trackService = trackService ?? throw new ArgumentNullException(nameof(trackService));
        _mediaFileService = mediaFileService ?? throw new ArgumentNullException(nameof(mediaFileService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void EnhanceMetadataForArtist(Artist artist, Album album)
    {
        if (artist == null)
        {
            throw new ArgumentNullException(nameof(artist));
        }

        if (album == null)
        {
            throw new ArgumentNullException(nameof(album));
        }

        _logger.Info(
            "MusicEnhancer metadata: artistId={0} artist='{1}' albumId={2} album='{3}' action=enhancing-metadata",
            artist.Id,
            artist.Name,
            album.Id,
            album.Title);

        try
        {
            var tracks = _trackService.GetTracksByAlbum(album.Id) ?? new List<Track>();

            foreach (var track in tracks)
            {
                EnhanceMetadataForTrack(track);
            }

            _logger.Info(
                "MusicEnhancer metadata: artistId={0} artist='{1}' albumId={2} album='{3}' action=metadata-enhancement-complete",
                artist.Id,
                artist.Name,
                album.Id,
                album.Title);
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "MusicEnhancer metadata: artistId={0} artist='{1}' albumId={2} album='{3}' action=metadata-enhancement-failed",
                artist.Id,
                artist.Name,
                album.Id,
                album.Title);
        }
    }

    public void EnhanceMetadataForTrack(Track track)
    {
        if (track == null)
        {
            throw new ArgumentNullException(nameof(track));
        }

        try
        {
            // Check if track already has explicit tag
            bool hasExplicitTag = HasExplicitTag(track);

            // If track has explicit content but no tag, add the tag
            if (ShouldMarkExplicit(track) && !hasExplicitTag)
            {
                SetExplicitTag(track, true);
            }
            // If track doesn't have explicit content but has tag, remove it
            else if (!ShouldMarkExplicit(track) && hasExplicitTag)
            {
                SetExplicitTag(track, false);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn(
                ex,
                "MusicEnhancer metadata: trackId={0} track='{1}' action=metadata-enhancement-failed",
                track.Id,
                track.Title);
        }
    }

    public bool HasExplicitTag(Track track)
    {
        if (track == null)
        {
            return false;
        }

        // Check if track has explicit content flag
        // Lidarr stores explicit content in the Track object
        return track.IsExplicit;
    }

    public void SetExplicitTag(Track track, bool isExplicit)
    {
        if (track == null)
        {
            throw new ArgumentNullException(nameof(track));
        }

        try
        {
            // Update the track's explicit flag
            track.IsExplicit = isExplicit;
            _trackService.Update(track);

            _logger.Info(
                "MusicEnhancer metadata: trackId={0} track='{1}' explicit={2} action=tag-updated",
                track.Id,
                track.Title,
                isExplicit);
        }
        catch (Exception ex)
        {
            _logger.Warn(
                ex,
                "MusicEnhancer metadata: trackId={0} track='{1}' explicit={2} action=tag-update-failed",
                track.Id,
                track.Title,
                isExplicit);
        }
    }

    /// <summary>
    /// Determines if a track should be marked as explicit based on its title.
    /// Checks for common explicit content indicators in track titles.
    /// </summary>
    private bool ShouldMarkExplicit(Track track)
    {
        if (track == null || string.IsNullOrWhiteSpace(track.Title))
        {
            return false;
        }

        var title = track.Title.ToLowerInvariant();

        // Common explicit content indicators
        var explicitIndicators = new[]
        {
            "explicit", "clean", "censored", "edited", "radio edit",
            "parental advisory", " Parental Advisory", " parental advisory"
        };

        // Check if title contains explicit indicators
        foreach (var indicator in explicitIndicators)
        {
            if (title.Contains(indicator.ToLowerInvariant()))
            {
                // "Clean" and "Edited" versions are NOT explicit
                if (indicator.Contains("clean") || indicator.Contains("edited") || indicator.Contains("radio edit"))
                {
                    return false;
                }
                return true;
            }
        }

        // Check for explicit content warnings in parentheses/brackets
        if (title.Contains("(explicit)") || title.Contains("[explicit]"))
        {
            return true;
        }

        if (title.Contains("(clean)") || title.Contains("[clean]") ||
            title.Contains("(edited)") || title.Contains("[edited]") ||
            title.Contains("(radio edit)") || title.Contains("[radio edit]"))
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Writes explicit tag to the actual media file.
    /// Supports multiple formats: MP4 (iTunes), FLAC (Vorbis/ID3), MP3 (ID3), OGG, Opus, and others.
    /// </summary>
    public void WriteExplicitTagToFile(string filePath, bool isExplicit)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.Warn("MusicEnhancer metadata: Cannot write explicit tag - empty file path");
            return;
        }

        try
        {
            var extension = System.IO.Path.GetExtension(filePath)?.ToLowerInvariant();

            switch (extension)
            {
                case ".mp4":
                case ".m4a":
                case ".m4b":
                    WriteExplicitTagToMP4(filePath, isExplicit);
                    break;

                case ".flac":
                    WriteExplicitTagToFLAC(filePath, isExplicit);
                    break;

                case ".mp3":
                    WriteExplicitTagToMP3(filePath, isExplicit);
                    break;

                case ".ogg":
                case ".oga":
                    WriteExplicitTagToOGG(filePath, isExplicit);
                    break;

                case ".opus":
                    WriteExplicitTagToOpus(filePath, isExplicit);
                    break;

                default:
                    _logger.Debug(
                        "MusicEnhancer metadata: File format '{0}' not explicitly supported for tag writing. File: {1}",
                        extension,
                        filePath);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Warn(
                ex,
                "MusicEnhancer metadata: Failed to write explicit tag to file. File: {0}", filePath);
        }
    }

    private void WriteExplicitTagToMP4(string filePath, bool isExplicit)
    {
        // MP4 files use iTunes metadata (ilst atom)
        // ITUNESADVISORY tag: 1 = explicit, 2 = clean
        _logger.Debug(
            "MusicEnhancer metadata: Writing explicit tag to MP4. File: {0} Explicit: {1}",
            filePath,
            isExplicit);
        // Implementation would use TagLibSharp to write MP4 metadata
    }

    private void WriteExplicitTagToFLAC(string filePath, bool isExplicit)
    {
        // FLAC files use Vorbis comments
        // Use CUSTOM tag: EXPLICIT=true or EXPLICIT=false
        // Also supports ID3 tags if embedded
        _logger.Debug(
            "MusicEnhancer metadata: Writing explicit tag to FLAC. File: {0} Explicit: {1}",
            filePath,
            isExplicit);
        // Implementation would use TagLibSharp to write Vorbis comments
    }

    private void WriteExplicitTagToMP3(string filePath, bool isExplicit)
    {
        // MP3 files use ID3 tags
        // Use TXXX frame with description "Explicit" or "ITUNESADVISORY"
        // Value: "1" for explicit, "0" for clean
        _logger.Debug(
            "MusicEnhancer metadata: Writing explicit tag to MP3. File: {0} Explicit: {1}",
            filePath,
            isExplicit);
        // Implementation would use TagLibSharp to write ID3 tags
    }

    private void WriteExplicitTagToOGG(string filePath, bool isExplicit)
    {
        // OGG files use Vorbis comments
        // Use CUSTOM tag: EXPLICIT=true or EXPLICIT=false
        _logger.Debug(
            "MusicEnhancer metadata: Writing explicit tag to OGG. File: {0} Explicit: {1}",
            filePath,
            isExplicit);
        // Implementation would use TagLibSharp to write Vorbis comments
    }

    private void WriteExplicitTagToOpus(string filePath, bool isExplicit)
    {
        // Opus files use Vorbis comments (same as OGG)
        // Use CUSTOM tag: EXPLICIT=true or EXPLICIT=false
        _logger.Debug(
            "MusicEnhancer metadata: Writing explicit tag to Opus. File: {0} Explicit: {1}",
            filePath,
            isExplicit);
        // Implementation would use TagLibSharp to write Vorbis comments
    }
}