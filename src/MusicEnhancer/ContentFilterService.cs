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
    HashSet<int> FilterContentForArtist(Artist artist, Album importedAlbum, MusicEnhancerOptions options);
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

    public HashSet<int> FilterContentForArtist(Artist artist, Album importedAlbum, MusicEnhancerOptions options)
    {
        var excludedTrackIds = new HashSet<int>();
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

        var isCompilation = importedAlbum.SecondaryTypes?.Any(t => string.Equals(t.Name, "Compilation", StringComparison.OrdinalIgnoreCase)) == true;
        if (!options.ReleaseTypesToFilter.Contains(albumType) && !(isCompilation && options.ReleaseTypesToFilter.Contains("Compilation")))
        {
            _logger.Debug("MusicEnhancer: Skipping album '{AlbumTitle}' because release type '{AlbumType}' is not enabled for filtering", importedAlbum.Title, albumType);
            return excludedTrackIds;
        }

        if (options.VersionFilterAction == VersionFilterAction.Skip)
        {
            _logger.Info("MusicEnhancer: Version filter action is 'Skip'; not checking album '{AlbumTitle}'", importedAlbum.Title);
            return excludedTrackIds;
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
                return excludedTrackIds;
            }

            var versionTracks = new List<Track>();

            foreach (var track in tracksWithFiles)
            {
                var versionInfo = VersionMatcher.MatchVersion(track);

                // Commentary often consists of very short spoken interludes.
                if (versionInfo?.VersionType != VersionType.Commentary &&
                    (track.Duration < options.MinimumTrackDurationMs || track.Duration > options.MaximumTrackDurationMs))
                {
                    continue;
                }

                if (versionInfo != null)
                {
                    if (ShouldFilterVersion(versionInfo.VersionType, options))
                    {
                        if (!HasCompatibleFile(track)) continue;
                        if (options.KeepCleanVersions && versionInfo.VersionType != VersionType.Commentary && HasCleanAdvisory(track) != false)
                        {
                            _logger.Debug("MusicEnhancer: Preserving track '{0}' with a clean or unverified advisory", track.Title);
                            continue;
                        }
                        if (options.VersionFilterAction == VersionFilterAction.Unmonitor && !HasVerifiedVersion(artist, track, versionInfo.VersionType))
                        {
                            _logger.Warn("MusicEnhancer: File for '{0}' has no matching version marker or recording ID; leaving it unchanged", track.Title);
                            continue;
                        }
                        versionTracks.Add(track);
                        excludedTrackIds.Add(track.Id);
                        _logger.Info("MusicEnhancer: Track '{TrackTitle}' detected as {VersionType} - {Reason}", track.Title, versionInfo.VersionType, versionInfo.Reason);
                    }
                }
            }

            if (versionTracks.Count == 0)
            {
                _logger.Debug("MusicEnhancer: No unwanted tracks found in album '{AlbumTitle}'", importedAlbum.Title);
                return excludedTrackIds;
            }

            if (options.VersionFilterAction != VersionFilterAction.Unmonitor)
            {
                _logger.Info(
                    "MusicEnhancer: Detected {Count} version track(s) in album '{AlbumTitle}'. Action is not 'Unmonitor', so no files will be deleted or unmonitored.",
                    versionTracks.Count,
                    importedAlbum.Title);
                return excludedTrackIds;
            }

            HandleCleanup(artist, importedAlbum, tracks, versionTracks, options);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MusicEnhancer: Error filtering content");
        }
        return excludedTrackIds;
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

    private bool HasCompatibleFile(Track track)
    {
        if (!Guid.TryParse(track.ForeignRecordingId, out _)) return true;
        try
        {
            var file = _mediaFileService.Get(track.TrackFileId);
            if (file != null && file.Id == track.TrackFileId && !string.IsNullOrWhiteSpace(file.Path) && System.IO.File.Exists(file.Path) &&
                MusicFileIdentity.HasNoRecordingIdConflict(file.Path, track)) return true;
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "MusicEnhancer: Unable to verify the recording for '{0}'", track.Title);
        }
        _logger.Warn("MusicEnhancer: File for '{0}' is missing or has a conflicting recording ID; skipping filter action", track.Title);
        return false;
    }

    private bool? HasCleanAdvisory(Track track)
    {
        try
        {
            var file = _mediaFileService.Get(track.TrackFileId);
            if (file == null || file.Id != track.TrackFileId || string.IsNullOrWhiteSpace(file.Path) || !System.IO.File.Exists(file.Path)) return null;
            return AdvisoryTagWriter.IsKnownClean(file.Path);
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "MusicEnhancer: Unable to inspect advisory for '{0}'", track.Title);
            return null;
        }
    }

    private bool HasVerifiedVersion(Artist artist, Track track, VersionType type)
    {
        try
        {
            var file = _mediaFileService.Get(track.TrackFileId);
            if (file == null || file.Id != track.TrackFileId || string.IsNullOrWhiteSpace(file.Path) || !System.IO.File.Exists(file.Path) || !MusicFileIdentity.IsWithinArtistFolder(artist, file.Path)) return false;
            using var audio = TagLib.File.Create(file.Path);
            if (Guid.TryParse(track.ForeignRecordingId, out var expected) &&
                Guid.TryParse(audio.Tag.MusicBrainzTrackId, out var existing) && expected == existing) return true;
            return !string.IsNullOrWhiteSpace(audio.Tag.Title) &&
                string.Equals(audio.Tag.Title.Trim(), track.Title?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                VersionMatcher.MatchVersion(new Track { Title = audio.Tag.Title })?.VersionType == type;
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "MusicEnhancer: Unable to verify version metadata for '{0}'", track.Title);
            return false;
        }
    }

    private void HandleCleanup(Artist artist, Album album, List<Track> allTracks, List<Track> versionTracks, MusicEnhancerOptions options)
    {
        // Never unmonitor an album containing unmatched or undownloaded tracks.
        if (allTracks.Count > 0 && versionTracks.Count == allTracks.Count)
        {
            _logger.Info("MusicEnhancer: Unmonitoring album '{0}' because all its tracks matched", album.Title);
            _albumService.SetAlbumMonitored(album.Id, false);
        }
        else
        {
            _logger.Info("MusicEnhancer: Album '{0}' contains unmatched or undownloaded tracks; leaving it monitored. Lidarr does not support individual track monitoring.", album.Title);
        }

        if (!options.DeleteFilesAfterUnmonitor) return;

        foreach (var track in versionTracks)
        {
            DeleteTrackFile(artist, track);
        }
    }

    private void DeleteTrackFile(Artist artist, Track track)
    {
        try
        {
            var linkedTracks = _trackService.GetTracksByFileId(track.TrackFileId);
            if (linkedTracks.Count != 1 || linkedTracks[0].Id != track.Id)
            {
                _logger.Warn("MusicEnhancer: Skipping shared or ambiguous file for track '{0}'", track.Title);
                return;
            }

            var trackFile = _mediaFileService.Get(track.TrackFileId);
            if (trackFile == null || trackFile.Id != track.TrackFileId || string.IsNullOrWhiteSpace(trackFile.Path))
            {
                _logger.Warn("MusicEnhancer: No track file found for '{0}'", track.Title);
                return;
            }

            if (!System.IO.File.Exists(trackFile.Path) || !MusicFileIdentity.HasNoRecordingIdConflict(trackFile.Path, track))
            {
                _logger.Warn("MusicEnhancer: Matched file for '{0}' is missing or has a conflicting recording ID; skipping deletion", track.Title);
                return;
            }

            if (!MusicFileIdentity.IsWithinArtistFolder(artist, trackFile.Path))
            {
                _logger.Warn("MusicEnhancer: Matched file for '{0}' is outside its artist folder; skipping deletion", track.Title);
                return;
            }

            // Use Lidarr's artist-aware deletion/recycle-bin path, not a user-facing reason as a folder name.
            _deleteMediaFiles.DeleteTrackFile(artist, trackFile);
            _logger.Info("MusicEnhancer: Deleted matched track file '{0}'", trackFile.Path);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MusicEnhancer: Failed to delete matched file for track '{0}'", track.Title);
        }
    }
}
