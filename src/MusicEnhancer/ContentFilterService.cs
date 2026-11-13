using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

/// <summary>
/// Service for filtering content based on version types.
/// Identifies remixes, edits, live versions, acoustic versions, etc.
/// Unmonitors albums and deletes unwanted track files.
/// </summary>
public interface IContentFilterService
{
    void FilterContentForArtist(Artist artist, Album importedAlbum, MusicEnhancerOptions options);
}

public class ContentFilterService : IContentFilterService
{
    private readonly Logger _logger;
    private readonly ITrackService _trackService;
    private readonly IMediaFileService _mediaFileService;
    private readonly IDeleteMediaFiles _deleteMediaFiles;
    private readonly IAlbumService _albumService;

    public ContentFilterService(Logger logger, ITrackService trackService, IMediaFileService mediaFileService, IDeleteMediaFiles deleteMediaFiles, IAlbumService albumService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _trackService = trackService ?? throw new ArgumentNullException(nameof(trackService));
        _mediaFileService = mediaFileService ?? throw new ArgumentNullException(nameof(mediaFileService));
        _deleteMediaFiles = deleteMediaFiles ?? throw new ArgumentNullException(nameof(deleteMediaFiles));
        _albumService = albumService ?? throw new ArgumentNullException(nameof(albumService));
    }

    public void FilterContentForArtist(Artist artist, Album importedAlbum, MusicEnhancerOptions options)
    {
        if (artist == null)
        {
            throw new ArgumentNullException(nameof(artist));
        }

        if (importedAlbum == null)
        {
            throw new ArgumentNullException(nameof(importedAlbum));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _logger.Info("MusicEnhancer: Filtering content for artist '{ArtistName}', album '{AlbumTitle}'", artist.Name, importedAlbum.Title);

        try
        {
            // Only process albums that have been downloaded (have track files)
            var tracks = _trackService.GetTracksByAlbum(importedAlbum.Id);
            var tracksWithFiles = tracks.Where(t => t.TrackFileId > 0).ToList();

            if (tracksWithFiles.Count == 0)
            {
                _logger.Info("MusicEnhancer: Album '{AlbumTitle}' has no downloaded tracks. Skipping.", importedAlbum.Title);
                return;
            }

            var tracksToDelete = new List<Track>();

            foreach (var track in tracksWithFiles)
            {
                var versionInfo = VersionMatcher.MatchVersion(track, options);

                if (versionInfo != null && versionInfo.VersionType != VersionType.None)
                {
                    if (ShouldFilterVersion(versionInfo.VersionType, options))
                    {
                        tracksToDelete.Add(track);
                        _logger.Info("MusicEnhancer: Track '{TrackTitle}' detected as {VersionType} - {Reason}", track.Title, versionInfo.VersionType, versionInfo.Reason);
                    }
                }
            }

            if (tracksToDelete.Count > 0)
            {
                HandleCleanup(artist, importedAlbum, tracksWithFiles, tracksToDelete, options);
            }
            else
            {
                _logger.Info("MusicEnhancer: No unwanted tracks found in album '{AlbumTitle}'", importedAlbum.Title);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MusicEnhancer: Error filtering content");
        }
    }

    private bool ShouldFilterVersion(VersionType versionType, MusicEnhancerOptions options)
    {
        return versionType switch
        {
            VersionType.Remix => options.EnableRemixFiltering,
            VersionType.Edit => options.EnableEditVersionFiltering,
            VersionType.Version => options.EnableVersionFiltering,
            VersionType.Live => options.EnableLiveVersionFiltering,
            VersionType.Acoustic => options.EnableAcousticVersionFiltering,
            VersionType.Demo => options.EnableDemoVersionFiltering,
            _ => false
        };
    }

    private void HandleCleanup(Artist artist, Album album, List<Track> allTracks, List<Track> tracksToDelete, MusicEnhancerOptions options)
    {
        // Calculate percentage of tracks that are unwanted
        var unwantedPercentage = (double)tracksToDelete.Count / allTracks.Count * 100;

        _logger.Info("MusicEnhancer: Album '{AlbumTitle}' has {Total} tracks, {Unwanted} are unwanted ({Percentage}%)",
            album.Title, allTracks.Count, tracksToDelete.Count, Math.Round(unwantedPercentage, 1));

        // If more than 50% of tracks are unwanted, unmonitor the entire album
        if (unwantedPercentage > 50)
        {
            _logger.Info("MusicEnhancer: Unmonitoring entire album '{AlbumTitle}' because {Percentage}% of tracks are unwanted",
                album.Title, Math.Round(unwantedPercentage, 1));

            // Delete all track files
            foreach (var track in allTracks)
            {
                DeleteTrackFile(track, "MusicEnhancer: Album unmonitored - version filter");
            }

            // Unmonitor the album
            _albumService.SetAlbumMonitored(album.Id, false);
        }
        else
        {
            // Only delete the unwanted tracks
            _logger.Info("MusicEnhancer: Deleting {Count} unwanted tracks from album '{AlbumTitle}'", tracksToDelete.Count, album.Title);

            foreach (var track in tracksToDelete)
            {
                DeleteTrackFile(track, "MusicEnhancer: Version filter - unwanted track");
            }
        }

        _logger.Info("MusicEnhancer: Cleanup complete for album '{AlbumTitle}'", album.Title);
    }

    private void DeleteTrackFile(Track track, string reason)
    {
        var trackFile = track.TrackFile?.Value;
        if (trackFile != null)
        {
            try
            {
                _deleteMediaFiles.DeleteTrackFile(trackFile, reason);
                _logger.Info("MusicEnhancer: Deleted track file '{FileName}'", trackFile.Path);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MusicEnhancer: Failed to delete track file '{FileName}'", trackFile.Path);
            }
        }
        else
        {
            _logger.Warn("MusicEnhancer: Track '{TrackTitle}' has no track file", track.Title);
        }
    }
}
