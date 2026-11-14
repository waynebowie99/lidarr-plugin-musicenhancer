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

        var albumType = string.IsNullOrWhiteSpace(importedAlbum.AlbumType) ? "Album" : importedAlbum.AlbumType;

        if (!options.ReleaseTypesToFilter.Contains(albumType))
        {
            _logger.Debug("MusicEnhancer: Skipping album '{AlbumTitle}' because release type '{AlbumType}' is not enabled for filtering", importedAlbum.Title, albumType);
            return;
        }

        if (options.VersionFilterAction == VersionFilterAction.Skip)
        {
            _logger.Info("MusicEnhancer: Version filter action is 'Skip'; not checking album '{AlbumTitle}'", importedAlbum.Title);
            return;
        }

        _logger.Debug("MusicEnhancer: Filtering content for artist '{ArtistName}', album '{AlbumTitle}'", artist.Name, importedAlbum.Title);

        try
        {
            // Only process albums that have been downloaded (have track files)
            var tracks = _trackService.GetTracksByAlbum(importedAlbum.Id) ?? new List<Track>();
            var tracksWithFiles = tracks.Where(t => t.TrackFileId > 0).ToList();

            if (tracksWithFiles.Count == 0)
            {
                _logger.Debug("MusicEnhancer: Album '{AlbumTitle}' has no downloaded tracks. Skipping.", importedAlbum.Title);
                return;
            }

            var tracksInDurationRange = tracksWithFiles
                .Where(t => t.Duration >= options.MinimumTrackDurationMs && t.Duration <= options.MaximumTrackDurationMs)
                .ToList();

            var versionTracks = new List<Track>();

            foreach (var track in tracksInDurationRange)
            {
                var versionInfo = VersionMatcher.MatchVersion(track, options);

                if (versionInfo != null && versionInfo.VersionType != VersionType.None)
                {
                    if (ShouldFilterVersion(versionInfo.VersionType, options))
                    {
                        versionTracks.Add(track);
                        _logger.Info("MusicEnhancer: Track '{TrackTitle}' detected as {VersionType} - {Reason}", track.Title, versionInfo.VersionType, versionInfo.Reason);
                    }
                }
            }

            if (versionTracks.Count == 0)
            {
                _logger.Debug("MusicEnhancer: No unwanted tracks found in album '{AlbumTitle}'", importedAlbum.Title);
                return;
            }

            if (options.VersionFilterAction == VersionFilterAction.FlagOnly)
            {
                _logger.Info(
                    "MusicEnhancer: Detected {Count} version track(s) in album '{AlbumTitle}'. Action is 'Flag for review', so no files will be deleted or unmonitored.",
                    versionTracks.Count,
                    importedAlbum.Title);
                return;
            }

            HandleCleanup(importedAlbum, tracksWithFiles, versionTracks, options);
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
            VersionType.Clean => !options.KeepCleanVersions,
            VersionType.Commentary => options.EnableCommentaryFiltering,
            _ => false
        };
    }

    private void HandleCleanup(Album album, List<Track> allTracks, List<Track> versionTracks, MusicEnhancerOptions options)
    {
        // Calculate percentage of tracks that are unwanted
        var unwantedPercentage = (double)versionTracks.Count / allTracks.Count * 100;

        _logger.Info("MusicEnhancer: Album '{AlbumTitle}' has {Total} tracks, {Unwanted} are unwanted ({Percentage}%)",
            album.Title, allTracks.Count, versionTracks.Count, Math.Round(unwantedPercentage, 1));

        // If more than 50% of tracks are unwanted, unmonitor the entire album
        if (unwantedPercentage > 50)
        {
            _logger.Info("MusicEnhancer: Unmonitoring entire album '{AlbumTitle}' because {Percentage}% of tracks are unwanted",
                album.Title, Math.Round(unwantedPercentage, 1));

            _albumService.SetAlbumMonitored(album.Id, false);

            if (options.DeleteFilesAfterUnmonitor)
            {
                foreach (var track in allTracks)
                {
                    DeleteTrackFile(track, "MusicEnhancer: Album unmonitored - version filter");
                }
            }
            else
            {
                _logger.Info("MusicEnhancer: 'Delete files after unmonitor' is disabled; keeping files for album '{AlbumTitle}'", album.Title);
            }
        }
        else
        {
            _logger.Info("MusicEnhancer: Album '{AlbumTitle}' contains mixed content; {Count} version track(s) detected", album.Title, versionTracks.Count);

            if (options.DeleteFilesAfterUnmonitor)
            {
                foreach (var track in versionTracks)
                {
                    DeleteTrackFile(track, "MusicEnhancer: Version filter - unwanted track");
                }
            }
            else
            {
                _logger.Info("MusicEnhancer: 'Delete files after unmonitor' is disabled; keeping version track files in album '{AlbumTitle}'", album.Title);
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
