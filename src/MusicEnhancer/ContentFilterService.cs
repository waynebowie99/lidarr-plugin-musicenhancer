using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

/// <summary>
/// Service for filtering content based on version types.
/// Identifies remixes, edits, live versions, acoustic versions, etc.
/// </summary>
public interface IContentFilterService
{
    void FilterContentForArtist(Artist artist, Album importedAlbum);
    FilterResult FilterWithOptions(Artist artist, Album importedAlbum, MusicEnhancerOptions options);
    FilterResult ScanArtistWithOptions(Artist artist, MusicEnhancerOptions options);
}

public class ContentFilterService : IContentFilterService
{
    private static readonly StringComparer AlbumTypeComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IAlbumService _albumService;
    private readonly ITrackService _trackService;
    private readonly IMediaFileService _mediaFileService;
    private readonly IDeleteMediaFiles _deleteMediaFiles;
    private readonly ICleanupHistoryRecorder _historyRecorder;
    private readonly Logger _logger;

    public ContentFilterService(
        IAlbumService albumService,
        ITrackService trackService,
        IMediaFileService mediaFileService,
        IDeleteMediaFiles deleteMediaFiles,
        ICleanupHistoryRecorder historyRecorder,
        Logger logger)
    {
        _albumService = albumService ?? throw new ArgumentNullException(nameof(albumService));
        _trackService = trackService ?? throw new ArgumentNullException(nameof(trackService));
        _mediaFileService = mediaFileService ?? throw new ArgumentNullException(nameof(mediaFileService));
        _deleteMediaFiles = deleteMediaFiles ?? throw new ArgumentNullException(nameof(deleteMediaFiles));
        _historyRecorder = historyRecorder ?? throw new ArgumentNullException(nameof(historyRecorder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void FilterContentForArtist(Artist artist, Album importedAlbum)
    {
        if (artist == null)
        {
            throw new ArgumentNullException(nameof(artist));
        }

        if (importedAlbum == null)
        {
            throw new ArgumentNullException(nameof(importedAlbum));
        }

        var options = CreateLegacyOptions();
        FilterWithOptions(artist, importedAlbum, options);
    }

    public FilterResult FilterWithOptions(Artist artist, Album importedAlbum, ContentFilterOptions options)
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

        if (!options.ShouldFilterReleaseType(importedAlbum.AlbumType))
        {
            _logger.Info(
                "MusicEnhancer skip: imported release type is not configured for filtering. artistId={0} artist='{1}' albumId={2} album='{3}' albumType='{4}' reason=unsupported-release-type",
                artist.Id,
                artist.Name,
                importedAlbum.Id,
                importedAlbum.Title,
                importedAlbum.AlbumType);
            return FilterResult.Empty;
        }

        var albums = GetAlbumsForArtist(artist);
        var albumsToFilter = albums.Where(a => a.Id != importedAlbum.Id && options.ShouldFilterReleaseType(a.AlbumType)).ToList();
        var candidateTracks = GetCandidateTracks(albumsToFilter, options);

        _logger.Info(
            "MusicEnhancer start: artistId={0} artist='{1}' importedAlbumId={2} importedAlbum='{3}' candidateReleases={4} candidateTracks={5} releaseTypes='{6}'",
            artist.Id,
            artist.Name,
            importedAlbum.Id,
            importedAlbum.Title,
            albumsToFilter.Count,
            candidateTracks.Values.Sum(t => t.Count),
            string.Join(",", options.ReleaseTypesToFilter.OrderBy(t => t)));

        var result = FilterResult.Empty;

        foreach (var album in albumsToFilter)
        {
            result += CheckAndFilterAlbum(artist, album, importedAlbum, candidateTracks, options);
        }

        return result;
    }

    public FilterResult ScanArtistWithOptions(Artist artist, ContentFilterOptions options)
    {
        if (artist == null)
        {
            throw new ArgumentNullException(nameof(artist));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var albums = GetAlbumsForArtist(artist);
        var albumsToFilter = albums.Where(a => options.ShouldFilterReleaseType(a.AlbumType)).ToList();
        var candidateTracks = GetCandidateTracks(albumsToFilter, options);

        _logger.Info(
            "MusicEnhancer scan start: artistId={0} artist='{1}' candidateReleases={2} candidateTracks={3} releaseTypes='{4}'",
            artist.Id,
            artist.Name,
            albumsToFilter.Count,
            candidateTracks.Values.Sum(t => t.Count),
            string.Join(",", options.ReleaseTypesToFilter.OrderBy(t => t)));

        var result = FilterResult.Empty;

        foreach (var album in albumsToFilter)
        {
            result += CheckAndFilterAlbum(artist, album, null, candidateTracks, options);
        }

        return result;
    }

    private FilterResult CheckAndFilterAlbum(Artist artist, Album album, Album? comparisonAlbumContext, Dictionary<int, List<Track>> candidateTracks, MusicEnhancerOptions options)
    {
        var candidatesChecked = 1;
        var albumsUnmonitored = 0;
        var tracksUnmonitored = 0;
        var skipped = 0;
        var reviewNeeded = 0;
        var unmonitorFailures = 0;
        var deleteFailures = 0;

        if (!album.Monitored)
        {
            _logger.Info(
                "MusicEnhancer skip: album already unmonitored. artistId={0} artist='{1}' albumId={2} album='{3}' reason=already-unmonitored",
                artist.Id,
                artist.Name,
                album.Id,
                album.Title);
            return new FilterResult(candidatesChecked, albumsUnmonitored, tracksUnmonitored, skipped, reviewNeeded, unmonitorFailures, deleteFailures);
        }

        List<Track> albumTracks;
        try
        {
            albumTracks = _trackService.GetTracksByAlbum(album.Id) ?? new List<Track>();
        }
        catch (Exception ex)
        {
            _logger.Warn(
                ex,
                "MusicEnhancer skip: failed to load album tracks. artistId={0} artist='{1}' albumId={2} album='{3}' reason=track-lookup-failed",
                artist.Id,
                artist.Name,
                album.Id,
                album.Title);
            return new FilterResult(candidatesChecked, albumsUnmonitored, tracksUnmonitored, skipped, reviewNeeded, unmonitorFailures, deleteFailures);
        }

        // Filter tracks by duration
        var filteredTracks = albumTracks.Where(t => 
            t.Duration >= options.MinimumTrackDurationMs && 
            t.Duration <= options.MaximumTrackDurationMs).ToList();

        if (filteredTracks.Count == 0)
        {
            _logger.Info(
                "MusicEnhancer skip: album has no tracks within duration range. artistId={0} artist='{1}' albumId={2} album='{3}' minDuration={4} maxDuration={5}",
                artist.Id,
                artist.Name,
                album.Id,
                album.Title,
                options.MinimumTrackDurationMs,
                options.MaximumTrackDurationMs);
            return new FilterResult(candidatesChecked, albumsUnmonitored, tracksUnmonitored, skipped, reviewNeeded, unmonitorFailures, deleteFailures);
        }

        // Check for versions in this album
        var versionResults = VersionMatcher.CheckVersions(filteredTracks, candidateTracks.Values.SelectMany(t => t), options);

        if (versionResults.Count == 0)
        {
            _logger.Info(
                "MusicEnhancer skip: no version matches found. artistId={0} artist='{1}' albumId={2} album='{3}'",
                artist.Id,
                artist.Name,
                album.Id,
                album.Title);
            return new FilterResult(candidatesChecked, albumsUnmonitored, tracksUnmonitored, skipped, reviewNeeded, unmonitorFailures, deleteFailures);
        }

        // Log version matches
        LogVersionMatches(artist, album, comparisonAlbumContext, versionResults, options);

        // Count versions vs originals
        var versionCount = versionResults.Count(r => r.VersionType != VersionType.None);
        var originalCount = versionResults.Count(r => r.VersionType == VersionType.None);

        // Check if any tracks should be unmonitored
        var tracksToUnmonitor = versionResults
            .Where(r => r.VersionType != VersionType.None)
            .Select(r => r.Track)
            .ToList();

        if (tracksToUnmonitor.Count == 0)
        {
            skipped = 1;
            return new FilterResult(candidatesChecked, albumsUnmonitored, tracksUnmonitored, reviewNeeded, unmonitorFailures, deleteFailures);
        }

        // Check if ALL tracks are versions (should unmonitor entire album)
        bool shouldUnmonitorAlbum = tracksToUnmonitor.Count == filteredTracks.Count;

        if (shouldUnmonitorAlbum)
        {
            // Unmonitor entire album
            string reason = $"All {versionResults.Count} tracks identified as versions (remixes, edits, etc.).";
            
            if (!TryUnmonitorAlbum(artist, album, comparisonAlbumContext, reason, options.DeleteFilesAfterUnmonitor))
            {
                unmonitorFailures = 1;
                return new FilterResult(candidatesChecked, albumsUnmonitored, tracksUnmonitored, reviewNeeded, unmonitorFailures, deleteFailures);
            }

            // The album is now unmonitored and its files are about to be deleted
            candidateTracks.Remove(album.Id);

            // Only delete files if the setting is enabled
            if (options.DeleteFilesAfterUnmonitor)
            {
                if (!TryDeleteAlbumFiles(artist, album, comparisonAlbumContext))
                {
                    albumsUnmonitored = 1; // Unmonitored successfully
                    deleteFailures = 1;
                    return new FilterResult(candidatesChecked, albumsUnmonitored, tracksUnmonitored, reviewNeeded, unmonitorFailures, deleteFailures);
                }
            }
            else
            {
                _logger.Info(
                    "MusicEnhancer skip delete: file deletion disabled by user setting. artistId={0} artist='{1}' albumId={2} album='{3}' reason=delete-disabled-by-config",
                    artist.Id,
                    artist.Name,
                    album.Id,
                    album.Title);
            }

            albumsUnmonitored = 1;
            return new FilterResult(candidatesChecked, albumsUnmonitored, tracksUnmonitored, reviewNeeded, unmonitorFailures, deleteFailures);
        }

        // Mixed content - unmonitor only the version tracks, keep originals
        if (versionCount > 0 && originalCount > 0)
        {
            // Unmonitor only the version tracks
            foreach (var track in tracksToUnmonitor)
            {
                if (!TryUnmonitorTrack(artist, album, track, comparisonAlbumContext, options.DeleteFilesAfterUnmonitor))
                {
                    unmonitorFailures = 1;
                }
                else
                {
                    tracksUnmonitored++;
                }
            }

            if (tracksUnmonitored > 0)
            {
                _logger.Info(
                    "MusicEnhancer partial-unmonitor: artistId={0} artist='{1}' albumId={2} album='{3}' unmonitoredTracks={4} remainingTracks={5}",
                    artist.Id,
                    artist.Name,
                    album.Id,
                    album.Title,
                    tracksUnmonitored,
                    originalCount);
            }

            reviewNeeded = 1; // Flag for review since it's mixed content
            return new FilterResult(candidatesChecked, albumsUnmonored, tracksUnmonitored, reviewNeeded, unmonitorFailures, deleteFailures);
        }

        // All tracks are versions - unmonitor entire album
        string allVersionReason = $"All {versionResults.Count} tracks identified as versions (remixes, edits, etc.).";
        
        if (!TryUnmonitorAlbum(artist, album, comparisonAlbumContext, allVersionReason, options.DeleteFilesAfterUnmonitor))
        {
            unmonitorFailures = 1;
            return new FilterResult(candidatesChecked, albumsUnmonitored, tracksUnmonitored, reviewNeeded, unmonitorFailures, deleteFailures);
        }

        // The album is now unmonitored and its files are about to be deleted
        candidateTracks.Remove(album.Id);

        // Only delete files if the setting is enabled
        if (options.DeleteFilesAfterUnmonitor)
        {
            if (!TryDeleteAlbumFiles(artist, album, comparisonAlbumContext))
            {
                albumsUnmonitored = 1; // Unmonitored successfully
                deleteFailures = 1;
                return new FilterResult(candidatesChecked, albumsUnmonitored, tracksUnmonitored, reviewNeeded, unmonitorFailures, deleteFailures);
            }
        }
        else
        {
            _logger.Info(
                "MusicEnhancer skip delete: file deletion disabled by user setting. artistId={0} artist='{1}' albumId={2} album='{3}' reason=delete-disabled-by-config",
                artist.Id,
                artist.Name,
                album.Id,
                album.Title);
        }

        albumsUnmonitored = 1;
        return new FilterResult(candidatesChecked, albumsUnmonitored, tracksUnmonitored, reviewNeeded, unmonitorFailures, deleteFailures);
    }

    private void LogVersionMatches(Artist artist, Album album, Album? comparisonAlbumContext, List<VersionMatchResult> versionResults, MusicEnhancerOptions options)
    {
        var versionCount = versionResults.Count(r => r.VersionType != VersionType.None);
        var originalCount = versionResults.Count(r => r.VersionType == VersionType.None);

        _logger.Info(
            "ContentFilter version-analysis: artistId={0} artist='{1}' albumId={2} album='{3}' totalTracks={4} versions={5} originals={6}",
            artist.Id,
            artist.Name,
            album.Id,
            album.Title,
            versionResults.Count,
            versionCount,
            originalCount);

        foreach (var result in versionResults)
        {
            _logger.Info(
                "ContentFilter track-version: artistId={0} artist='{1}' albumId={2} album='{3}' track='{4}' versionType={5} confidence={6} reason='{7}'",
                artist.Id,
                artist.Name,
                album.Id,
                album.Title,
                result.Track.Title,
                result.VersionType,
                result.Confidence,
                result.Reason);
        }
    }

    private Dictionary<int, List<Track>> GetCandidateTracks(List<Album> albums, MusicEnhancerOptions options)
    {
        var tracks = new Dictionary<int, List<Track>>();

        foreach (var album in albums.Where(a => a.Monitored))
        {
            try
            {
                var albumTracks = _trackService.GetTracksByAlbum(album.Id) ?? new List<Track>();
                
                // Filter tracks by duration
                var filteredTracks = albumTracks.Where(t => 
                    t.Duration >= options.MinimumTrackDurationMs && 
                    t.Duration <= options.MaximumTrackDurationMs).ToList();

                if (filteredTracks.Count > 0)
                {
                    tracks[album.Id] = filteredTracks;
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(
                    ex,
                    "ContentFilter lookup failure: failed to load album tracks. albumId={0} album='{1}' albumType='{2}' reason=track-lookup-failed",
                    album.Id,
                    album.Title,
                    album.AlbumType);
            }
        }

        return tracks;
    }

    private List<Album> GetAlbumsForArtist(Artist artist)
    {
        try
        {
            return _albumService.GetAlbumsByArtist(artist.Id) ?? new List<Album>();
        }
        catch (Exception ex)
        {
            _logger.Warn(
                ex,
                "ContentFilter lookup failure: failed to load artist albums. artistId={0} artist='{1}' reason=album-lookup-failed",
                artist.Id,
                artist.Name);
            return new List<Album>();
        }
    }

    private bool TryUnmonitorAlbum(Artist artist, Album album, Album? comparisonAlbumContext, string reason, bool deleteFiles = true)
    {
        try
        {
            _albumService.SetAlbumMonitored(album.Id, false);
            album.Monitored = false;
            
            string logReason = deleteFiles 
                ? reason + " Files will be deleted." 
                : reason + " Files will be kept (deletion disabled by user setting).";
            
            _logger.Info(
                "ContentFilter unmonitor success: artistId={0} artist='{1}' albumId={2} album='{3}' reason='{4}'",
                artist.Id,
                artist.Name,
                album.Id,
                album.Title,
                logReason);
            
            try
            {
                _historyRecorder.RecordUnmonitor(artist, album, logReason);
            }
            catch (Exception historyEx)
            {
                _logger.Warn(
                    historyEx,
                    "ContentFilter history: action=unmonitor-record-failed artistId={0} albumId={1} reason=history-recorder-threw",
                    artist.Id,
                    album.Id);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Warn(
                ex,
                "ContentFilter unmonitor failure: artistId={0} artist='{1}' albumId={2} album='{3}' reason=unmonitor-failed-delete-skipped",
                artist.Id,
                artist.Name,
                album.Id,
                album.Title);
            return false;
        }
    }

    private bool TryDeleteAlbumFiles(Artist artist, Album album, Album? comparisonAlbumContext)
    {
        List<TrackFile> files;
        try
        {
            files = _mediaFileService.GetFilesByAlbum(album.Id) ?? new List<TrackFile>();
        }
        catch (Exception ex)
        {
            _logger.Warn(
                ex,
                "ContentFilter delete skip: failed to load media files after unmonitor. artistId={0} artist='{1}' albumId={2} album='{3}' reason=media-file-lookup-failed",
                artist.Id,
                artist.Name,
                album.Id,
                album.Title);
            return false;
        }

        if (files.Count == 0)
        {
            _logger.Info(
                "ContentFilter delete skip: no media files to delete after unmonitor. artistId={0} artist='{1}' albumId={2} album='{3}' reason=no-media-files",
                artist.Id,
                artist.Name,
                album.Id,
                album.Title);
            return true;
        }

        var allDeleted = true;
        foreach (var file in files)
        {
            try
            {
                _deleteMediaFiles.DeleteTrackFile(artist, file);

                _logger.Info(
                    "ContentFilter delete success: artistId={0} artist='{1}' albumId={2} album='{3}' trackFileId={4} path='{5}'",
                    artist.Id,
                    artist.Name,
                    album.Id,
                    album.Title,
                    file.Id,
                    file.Path);
            }
            catch (Exception ex)
            {
                allDeleted = false;
                _logger.Warn(
                    ex,
                    "ContentFilter delete failure: album remains safely unmonitored. artistId={0} artist='{1}' albumId={2} album='{3}' trackFileId={4} path='{5}' reason=delete-failed",
                    artist.Id,
                    artist.Name,
                    album.Id,
                    album.Title,
                    file.Id,
                    file.Path);
            }
        }

        return allDeleted;
    }

    private bool TryUnmonitorTrack(Artist artist, Album album, Track track, Album? comparisonAlbumContext, bool deleteFiles = true)
    {
        try
        {
            // Unmonitor the track
            var trackFile = track.TrackFileId.HasValue 
                ? _mediaFileService.GetFile(track.TrackFileId.Value) 
                : null;

            if (trackFile != null)
            {
                // Mark track file as ignored so Lidarr won't re-download it
                trackFile.Quality = null; // This will cause Lidarr to ignore this file
                _mediaFileService.Update(trackFile);
            }

            // Update the track's monitored status
            track.Monitored = false;
            _trackService.Update(track);

            string logReason = deleteFiles 
                ? "Track is a version (remix/edit/etc.). File will be deleted." 
                : "Track is a version (remix/edit/etc.). File will be kept (deletion disabled by user setting).";

            _logger.Info(
                "MusicEnhancer track unmonitor success: artistId={0} artist='{1}' albumId={2} album='{3}' trackId={4} track='{5}' reason='{6}'",
                artist.Id,
                artist.Name,
                album.Id,
                album.Title,
                track.Id,
                track.Title,
                logReason);

            try
            {
                // Record in history that this track was unmonitored
                _historyRecorder.RecordUnmonitor(artist, album, $"Track '{track.Title}' unmonitored: {logReason}");
            }
            catch (Exception historyEx)
            {
                _logger.Warn(
                    historyEx,
                    "MusicEnhancer history: action=track-unmonitor-record-failed artistId={0} trackId={1} reason=history-recorder-threw",
                    artist.Id,
                    track.Id);
            }

            // If delete is enabled, delete the file
            if (deleteFiles && trackFile != null)
            {
                try
                {
                    _deleteMediaFiles.DeleteTrackFile(artist, trackFile);
                    _logger.Info(
                        "MusicEnhancer track delete success: artistId={0} artist='{1}' albumId={2} album='{3}' trackId={4} track='{5}' trackFileId={6} path='{7}'",
                        artist.Id,
                        artist.Name,
                        album.Id,
                        album.Title,
                        track.Id,
                        track.Title,
                        trackFile.Id,
                        trackFile.Path);
                }
                catch (Exception deleteEx)
                {
                    _logger.Warn(
                        deleteEx,
                        "MusicEnhancer track delete failure: track remains unmonitored. artistId={0} artist='{1}' albumId={2} album='{3}' trackId={4} track='{5}' trackFileId={6} path='{7}' reason=delete-failed",
                        artist.Id,
                        artist.Name,
                        album.Id,
                        album.Title,
                        track.Id,
                        track.Title,
                        trackFile.Id,
                        trackFile.Path);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Warn(
                ex,
                "MusicEnhancer track unmonitor failure: artistId={0} artist='{1}' albumId={2} album='{3}' trackId={4} track='{5}' reason=unmonitor-failed",
                artist.Id,
                artist.Name,
                album.Id,
                album.Title,
                track.Id,
                track.Title);
            return false;
        }
    }

    private static MusicEnhancerOptions CreateLegacyOptions()
    {
        return new MusicEnhancerOptions(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Album", "EP" },
            VersionFilterAction.FlagOnly,
            3000,
            deleteFilesAfterUnmonitor: true);
    }
}
