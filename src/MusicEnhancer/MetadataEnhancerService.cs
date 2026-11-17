using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Music;
using TagLib;
using TagLib.Id3v2;
using TagLib.Mpeg4;
using TagLib.Ogg;

namespace NzbDrone.Core.Plugins;

public interface IMetadataEnhancerService
{
    void EnhanceMetadataForArtist(Artist artist, Album album, MusicEnhancerOptions options, ISet<int>? excludedTrackIds = null);
}

public class MetadataEnhancerService : IMetadataEnhancerService
{
    private readonly ITrackService _tracks;
    private readonly IMediaFileService _files;
    private readonly IExplicitLookupService _lookup;
    private readonly IAdditionalMetadataLookupService _additionalLookup;
    private readonly IRootFolderWatchingService _watcher;
    private readonly Logger _logger;
    private static readonly object WriteLock = new();

    public MetadataEnhancerService(ITrackService tracks, IMediaFileService files, IExplicitLookupService lookup, IAdditionalMetadataLookupService additionalLookup, IRootFolderWatchingService watcher, Logger logger)
    {
        _tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        _additionalLookup = additionalLookup ?? throw new ArgumentNullException(nameof(additionalLookup));
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void EnhanceMetadataForArtist(Artist artist, Album album, MusicEnhancerOptions options, ISet<int>? excludedTrackIds = null)
    {
        ArgumentNullException.ThrowIfNull(artist);
        ArgumentNullException.ThrowIfNull(album);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.EnableMetadataProcessing || !options.EnableMetadataWriting ||
            (!options.EnableExplicitTagging && !options.EnableLibraryTagging && !options.EnableLyricsLookup))
        {
            return;
        }

        // Do database/path checks serially; perform only independent external lookups in
        // the bounded workers below. Filtered tracks are excluded before any network call.
        var candidates = new List<(Track Track, TrackFile File)>();
        foreach (var track in _tracks.GetTracksByAlbum(album.Id).Where(t => t.TrackFileId > 0))
        {
            try
            {
                if (excludedTrackIds?.Contains(track.Id) == true)
                {
                    _logger.Debug("MusicEnhancer: Skipping metadata lookup for filtered track '{0}'", track.Title);
                    continue;
                }

                // A single audio file may contain multiple tracks; it cannot safely receive a per-track rating.
                var associatedTracks = _tracks.GetTracksByFileId(track.TrackFileId);
                if (associatedTracks.Count != 1 || associatedTracks[0].Id != track.Id)
                {
                    _logger.Debug("MusicEnhancer: Skipping shared or ambiguous file for track '{0}'", track.Title);
                    continue;
                }

                var file = _files.Get(track.TrackFileId);
                if (file == null || file.Id != track.TrackFileId || string.IsNullOrWhiteSpace(file.Path) || !System.IO.File.Exists(file.Path) ||
                    !LibraryMetadataWriter.Supports(file.Path) || !MusicFileIdentity.IsWithinArtistFolder(artist, file.Path))
                {
                    continue;
                }

                lock (WriteLock)
                {
                    if (!MusicFileIdentity.HasNoRecordingIdConflict(file.Path, track))
                    {
                        _logger.Warn("MusicEnhancer: Recording ID in '{0}' conflicts with Lidarr; leaving metadata unchanged", file.Path);
                        continue;
                    }
                }
                candidates.Add((track, file));
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "MusicEnhancer: Unable to prepare metadata for track '{0}'", track.Title);
            }
        }

        Parallel.ForEach(candidates, new ParallelOptions { MaxDegreeOfParallelism = 4 }, candidate =>
        {
            try
            {
                ProcessTrack(artist, album, candidate.Track, candidate.File, options);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "MusicEnhancer: Unable to tag track '{0}'", candidate.Track.Title);
            }
        });
    }

    private void ProcessTrack(Artist artist, Album album, Track track, TrackFile file, MusicEnhancerOptions options)
    {
        bool hasAdvisory = false;
        bool hasLyrics = true;
        bool missingBpm = false;
        bool missingComposers = false;
        bool missingMood = false;
        bool missingRecordingMetadata = false;
        string? isrc = null;

        lock (WriteLock)
        {
            using var audio = TagLib.File.Create(file.Path);
            if (options.EnableExplicitTagging) hasAdvisory = AdvisoryTagWriter.HasRating(file.Path);
            if (options.EnableLyricsLookup) hasLyrics = LyricsSidecarWriter.HasLyrics(file.Path);
            if (options.EnableLibraryTagging)
            {
                missingBpm = audio.Tag.BeatsPerMinute == 0;
                missingComposers = !audio.Tag.Composers.Any(s => !string.IsNullOrWhiteSpace(s));
                missingMood = !LibraryMetadataWriter.HasMood(audio);
                missingRecordingMetadata = LibraryMetadataWriter.NeedsRecordingMetadata(audio, file.Path);
                isrc = audio.Tag.ISRC;
            }
        }

        var clean = VersionMatcher.MatchVersion(track)?.VersionType == VersionType.Clean;
        var conflictingAdvisory = clean && track.Explicit;
        int? rating = conflictingAdvisory ? null : clean ? 2 : track.Explicit ? 1 : null;
        if (conflictingAdvisory)
        {
            _logger.Warn("MusicEnhancer: Conflicting clean and explicit metadata for '{0}'; leaving advisory unchanged", track.Title);
        }

        var needsOnlineRating = options.EnableExplicitTagging && !hasAdvisory && !conflictingAdvisory && rating == null && options.EnableOnlineExplicitLookup;
        var needsRecording = options.EnableLibraryTagging &&
                             ((options.EnableRecordingMetadataLookup && missingRecordingMetadata) ||
                              (options.EnableComposerLookup && missingComposers) ||
                              needsOnlineRating ||
                              (options.EnableBpmLookup && missingBpm && string.IsNullOrWhiteSpace(isrc)));

        // These independent services run concurrently. MusicBrainz's shared throttle
        // still serializes its requests to comply with its one-request-per-second limit.
        var recordingTask = needsRecording
            ? Task.Run(() => _additionalLookup.LookupRecordingMetadata(track))
            : Task.FromResult<RecordingMetadata?>(null);
        var lyricsTask = options.EnableLyricsLookup && !hasLyrics
            ? Task.Run(() => _additionalLookup.LookupLyrics(track.ArtistMetadata?.Value?.Name, album.Title, track.Title, track.Duration))
            : Task.FromResult<string?>(null);
        var moodTask = options.EnableMoodLookup && options.EnableLibraryTagging && missingMood
            ? Task.Run(() => _additionalLookup.LookupMoods(track, options.LastFmApiKey))
            : Task.FromResult<string[]?>(null);
        var bpmTask = options.EnableBpmLookup && options.EnableLibraryTagging && missingBpm && !string.IsNullOrWhiteSpace(isrc)
            ? Task.Run(() => _additionalLookup.LookupBpm(isrc, track.Duration))
            : Task.FromResult<uint?>(null);

        Task.WaitAll(recordingTask, lyricsTask, moodTask, bpmTask);
        var recording = recordingTask.Result;
        var lyrics = lyricsTask.Result;
        var moods = moodTask.Result;
        var bpm = bpmTask.Result;
        var composers = options.EnableComposerLookup && options.EnableLibraryTagging && missingComposers
            ? recording?.Composers
            : null;
        if (options.EnableBpmLookup && options.EnableLibraryTagging && missingBpm && bpm == null &&
            string.IsNullOrWhiteSpace(isrc) && recording?.Isrcs?.Length == 1)
        {
            bpm = _additionalLookup.LookupBpm(recording.Isrcs[0], track.Duration);
        }
        if (options.EnableRecordingMetadataLookup && missingRecordingMetadata) moods ??= recording?.Moods;
        var recordingToWrite = options.EnableRecordingMetadataLookup && missingRecordingMetadata ? recording : null;

        if (needsOnlineRating)
        {
            rating = (int?)_lookup.Lookup(track, recording, needsRecording);
        }

        if (options.EnableLibraryTagging)
        {
            lock (WriteLock)
            {
                if (LibraryMetadataWriter.WriteIfMissing(file.Path, artist, album, track, bpm, composers, moods, recordingToWrite,
                    () => _watcher.ReportFileSystemChangeBeginning(file.Path))) UpdateFile(file);
            }
        }

        if (lyrics != null)
        {
            lock (WriteLock)
            {
                if (LyricsSidecarWriter.WriteIfMissing(file.Path, lyrics,
                    () => _watcher.ReportFileSystemChangeBeginning(Path.ChangeExtension(file.Path, ".lrc"))))
                    _logger.Info("MusicEnhancer: Added lyrics sidecar for '{0}'", track.Title);
            }
        }

        if (options.EnableExplicitTagging && !hasAdvisory && rating.HasValue)
        {
            lock (WriteLock)
            {
                if (AdvisoryTagWriter.WriteIfMissing(file.Path, rating.Value,
                    () => _watcher.ReportFileSystemChangeBeginning(file.Path)))
                {
                    UpdateFile(file);
                    _logger.Info("MusicEnhancer: Tagged '{0}' as {1}", track.Title, rating == 1 ? "explicit" : "clean");
                }
            }
        }
    }

    private void UpdateFile(TrackFile file)
    {
        var info = new FileInfo(file.Path);
        file.Size = info.Length;
        file.Modified = info.LastWriteTimeUtc;
        _files.Update(file);
    }
}

/// <summary>Writes only an absent advisory field; leaves other tags untouched.</summary>
public static class AdvisoryTagWriter
{
    private static readonly ByteVector RatingAtom = ByteVector.FromString("rtng", StringType.Latin1);

    public static bool HasRating(string path)
    {
        using var file = TagLib.File.Create(path);
        return HasRating(file);
    }

    public static bool IsKnownClean(string path)
    {
        using var file = TagLib.File.Create(path);
        var clean = false;

        if (!string.IsNullOrWhiteSpace(file.Tag.Title) &&
            VersionMatcher.MatchVersion(new Track { Title = file.Tag.Title })?.VersionType == VersionType.Clean) clean = true;

        void Check(string? value, bool explicitField = false)
        {
            var text = value?.Trim();
            if (string.IsNullOrEmpty(text)) return;
            if (!explicitField && (text == "2" || text.Equals("clean", StringComparison.OrdinalIgnoreCase))) clean = true;
        }

        if (file.GetTag(TagTypes.Xiph) is XiphComment xiph)
        {
            foreach (var name in new[] { "ITUNESADVISORY", "RTNG", "EXPLICIT" })
                foreach (var value in xiph.GetField(name)) Check(value, name == "EXPLICIT");
        }
        if (file.GetTag(TagTypes.Id3v2) is TagLib.Id3v2.Tag id3)
        {
            foreach (var frame in id3.GetFrames<UserTextInformationFrame>())
            {
                if (!new[] { "ITUNESADVISORY", "RTNG", "EXPLICIT" }.Any(name => string.Equals(frame.Description, name, StringComparison.OrdinalIgnoreCase))) continue;
                foreach (var value in frame.Text) Check(value, string.Equals(frame.Description, "EXPLICIT", StringComparison.OrdinalIgnoreCase));
            }
        }
        if (file.GetTag(TagTypes.Apple) is AppleTag apple)
        {
            foreach (var box in apple.DataBoxes(RatingAtom))
                if (box.Data.Count > 0) Check(box.Data[box.Data.Count - 1].ToString());
            foreach (var value in AppleTagHelpers.ReadFreeform(apple, "ITUNESADVISORY", "RTNG")) Check(value);
            foreach (var value in AppleTagHelpers.ReadFreeform(apple, "EXPLICIT")) Check(value, true);
        }
        // A clean marker takes precedence for file safety, even if another tag conflicts.
        return clean;
    }

    private static bool HasRating(TagLib.File file)
    {
        if (file.GetTag(TagTypes.Xiph) is XiphComment xiph &&
            new[] { "ITUNESADVISORY", "EXPLICIT", "RTNG" }.Any(name => xiph.GetField(name).Length > 0))
        {
            return true;
        }

        if (file.GetTag(TagTypes.Id3v2) is TagLib.Id3v2.Tag id3 &&
            id3.GetFrames<UserTextInformationFrame>().Any(frame =>
                new[] { "ITUNESADVISORY", "EXPLICIT", "RTNG" }.Any(name =>
                    string.Equals(frame.Description, name, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        if (file.GetTag(TagTypes.Apple) is AppleTag apple &&
            (apple.DataBoxes(RatingAtom).Any() || AppleTagHelpers.HasFreeform(apple, "ITUNESADVISORY", "EXPLICIT", "RTNG")))
        {
            return true;
        }

        return false;
    }

    public static bool WriteIfMissing(string path, int rating, Action? beforeSave = null)
    {
        if (rating != 1 && rating != 2)
        {
            throw new ArgumentOutOfRangeException(nameof(rating));
        }

        using var file = TagLib.File.Create(path);
        if (HasRating(file))
        {
            return false;
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        switch (extension)
        {
            case ".flac":
            case ".ogg":
            case ".opus":
                if (file.GetTag(TagTypes.Xiph, true) is not XiphComment xiph) return false;
                xiph.SetField("ITUNESADVISORY", rating.ToString());
                break;
            case ".mp3":
                if (file.GetTag(TagTypes.Id3v2, true) is not TagLib.Id3v2.Tag id3) return false;
                UserTextInformationFrame.Get(id3, "ITUNESADVISORY", true).Text = new[] { rating.ToString() };
                break;
            case ".m4a":
            case ".mp4":
                if (file.GetTag(TagTypes.Apple, true) is not AppleTag apple) return false;
                // Apple rtng uses 4 for explicit and 2 for clean, not iTunes' 1/2.
                apple.SetData(RatingAtom, new ByteVector(new[] { (byte)(rating == 1 ? 4 : 2) }), 21);
                break;
            default:
                return false;
        }

        beforeSave?.Invoke();
        file.Save();
        return true;
    }
}
