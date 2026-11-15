using System;
using System.IO;
using System.Linq;
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
    void EnhanceMetadataForArtist(Artist artist, Album album, MusicEnhancerOptions options);
}

public class MetadataEnhancerService : IMetadataEnhancerService
{
    private readonly ITrackService _tracks;
    private readonly IMediaFileService _files;
    private readonly IExplicitLookupService _lookup;
    private readonly Logger _logger;
    private static readonly object WriteLock = new();

    public MetadataEnhancerService(ITrackService tracks, IMediaFileService files, IExplicitLookupService lookup, Logger logger)
    {
        _tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void EnhanceMetadataForArtist(Artist artist, Album album, MusicEnhancerOptions options)
    {
        ArgumentNullException.ThrowIfNull(artist);
        ArgumentNullException.ThrowIfNull(album);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.EnableMetadataProcessing || !options.EnableExplicitTagging || !options.EnableMetadataWriting)
        {
            return;
        }

        foreach (var track in _tracks.GetTracksByAlbum(album.Id).Where(t => t.TrackFileId > 0))
        {
            try
            {
                // A single audio file may contain multiple tracks; it cannot safely receive a per-track rating.
                var associatedTracks = _tracks.GetTracksByFileId(track.TrackFileId);
                if (associatedTracks.Count != 1 || associatedTracks[0].Id != track.Id)
                {
                    _logger.Debug("MusicEnhancer: Skipping shared or ambiguous file for track '{0}'", track.Title);
                    continue;
                }

                var file = _files.Get(track.TrackFileId);
                if (file == null || string.IsNullOrWhiteSpace(file.Path) || !System.IO.File.Exists(file.Path))
                {
                    continue;
                }

                // Preserve manual and existing ratings, even if they conflict with the metadata.
                lock (WriteLock)
                {
                    if (AdvisoryTagWriter.HasRating(file.Path))
                    {
                        continue;
                    }
                }

                var clean = VersionMatcher.MatchVersion(track, options)?.VersionType == VersionType.Clean;
                // Clean and explicit evidence can conflict; don't guess.
                if (clean && track.Explicit)
                {
                    _logger.Warn("MusicEnhancer: Conflicting clean and explicit metadata for '{0}'; leaving tags unchanged", track.Title);
                    continue;
                }

                int? rating = clean ? 2 : track.Explicit ? 1 : null;
                if (rating == null && options.EnableOnlineExplicitLookup)
                {
                    rating = (int?)_lookup.Lookup(track);
                }

                if (rating == null)
                {
                    continue;
                }

                lock (WriteLock)
                {
                    // Recheck because another import or startup scan may have written it during the lookup.
                    if (!AdvisoryTagWriter.WriteIfMissing(file.Path, rating.Value))
                    {
                        continue;
                    }

                    var info = new FileInfo(file.Path);
                    file.Size = info.Length;
                    file.Modified = info.LastWriteTimeUtc;
                    _files.Update(file);
                }

                _logger.Info("MusicEnhancer: Tagged '{0}' as {1}", track.Title, rating == 1 ? "explicit" : "clean");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "MusicEnhancer: Unable to tag track '{0}'", track.Title);
            }
        }
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
            (apple.DataBoxes(RatingAtom).Any() || apple.GetDashBox("com.apple.iTunes", "ITUNESADVISORY") != null ||
             apple.GetDashBox("com.apple.iTunes", "EXPLICIT") != null))
        {
            return true;
        }

        return false;
    }

    public static bool WriteIfMissing(string path, int rating)
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

        file.Save();
        return true;
    }
}
