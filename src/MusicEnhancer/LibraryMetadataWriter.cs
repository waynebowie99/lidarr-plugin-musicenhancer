using System;
using System.Linq;
using System.IO;
using NzbDrone.Core.Music;
using TagLib;
using TagLib.Id3v2;

namespace NzbDrone.Core.Plugins;

/// <summary>Copies known Lidarr metadata into empty standard audio fields only.</summary>
public static class LibraryMetadataWriter
{
    public static bool Supports(string path) => Path.GetExtension(path).ToLowerInvariant() is ".mp3" or ".flac" or ".ogg" or ".opus" or ".m4a" or ".mp4";

    public static bool WriteIfMissing(string path, Artist albumArtist, Album album, Track track, uint? bpm = null, string[]? composers = null, string[]? moods = null)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (!Supports(path)) return false;
        using var audio = TagLib.File.Create(path);
        var tag = audio.Tag;
        var release = track.AlbumRelease?.Value;
        var trackArtist = track.ArtistMetadata?.Value;
        var changed = false;

        void SetText(string? oldValue, string? newValue, Action<string> setter)
        {
            if (string.IsNullOrWhiteSpace(oldValue) && !string.IsNullOrWhiteSpace(newValue))
            {
                setter(newValue);
                changed = true;
            }
        }

        void SetArray(string[]? oldValue, string[]? newValue, Action<string[]> setter)
        {
            var clean = newValue?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if ((oldValue == null || !oldValue.Any(s => !string.IsNullOrWhiteSpace(s))) && clean?.Length > 0)
            {
                setter(clean);
                changed = true;
            }
        }

        void SetNumber(uint oldValue, int newValue, Action<uint> setter)
        {
            if (oldValue == 0 && newValue > 0)
            {
                setter((uint)newValue);
                changed = true;
            }
        }

        static string? Id(string? id) => Guid.TryParse(id, out var guid) ? guid.ToString("D") : null;

        SetText(tag.Title, track.Title, value => tag.Title = value);
        SetText(tag.Album, album.Title, value => tag.Album = value);
        SetArray(tag.Performers, new[] { trackArtist?.Name ?? string.Empty }, value => tag.Performers = value);
        SetArray(tag.AlbumArtists, new[] { albumArtist.Name ?? string.Empty }, value => tag.AlbumArtists = value);
        SetArray(tag.AlbumArtistsSort, new[] { albumArtist.SortName ?? string.Empty }, value => tag.AlbumArtistsSort = value);
        var genres = album.Genres?.Count > 0 ? album.Genres : trackArtist?.Genres;
        SetArray(tag.Genres, genres?.ToArray(), value => tag.Genres = value);
        SetArray(tag.Composers, composers, value => tag.Composers = value);
        SetNumber(tag.Track, track.AbsoluteTrackNumber, value => tag.Track = value);
        SetNumber(tag.Disc, track.MediumNumber, value => tag.Disc = value);
        SetNumber(tag.DiscCount, release?.Media?.Count ?? 0, value => tag.DiscCount = value);
        SetNumber(tag.Year, release?.ReleaseDate?.Year ?? album.ReleaseDate?.Year ?? 0, value => tag.Year = value);
        var label = release?.Label?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(tag.Publisher))
        {
            if (audio.GetTag(TagTypes.Xiph) is TagLib.Ogg.XiphComment xiph)
            {
                if (new[] { "LABEL", "ORGANIZATION", "PUBLISHER" }.All(key => xiph.GetField(key).Length == 0))
                {
                    xiph.SetField("LABEL", label);
                    changed = true;
                }
            }
            else if (audio.GetTag(TagTypes.Apple) is TagLib.Mpeg4.AppleTag apple)
            {
                if (apple.GetDashBox("com.apple.iTunes", "LABEL") == null)
                {
                    apple.SetDashBox("com.apple.iTunes", "LABEL", label);
                    changed = true;
                }
            }
            else
            {
                tag.Publisher = label;
                changed = true;
            }
        }
        SetText(tag.MusicBrainzTrackId, Id(track.ForeignRecordingId), value => tag.MusicBrainzTrackId = value);
        SetText(tag.MusicBrainzArtistId, Id(trackArtist?.ForeignArtistId), value => tag.MusicBrainzArtistId = value);
        SetText(tag.MusicBrainzReleaseArtistId, Id(albumArtist.ForeignArtistId), value => tag.MusicBrainzReleaseArtistId = value);
        SetText(tag.MusicBrainzReleaseGroupId, Id(album.ForeignAlbumId), value => tag.MusicBrainzReleaseGroupId = value);
        SetText(tag.MusicBrainzReleaseId, Id(release?.ForeignReleaseId), value => tag.MusicBrainzReleaseId = value);
        SetText(tag.MusicBrainzReleaseType, album.AlbumType, value => tag.MusicBrainzReleaseType = value);
        SetText(tag.MusicBrainzReleaseStatus, release?.Status, value => tag.MusicBrainzReleaseStatus = value);
        if (bpm is > 0 && tag.BeatsPerMinute == 0)
        {
            tag.BeatsPerMinute = bpm.Value;
            changed = true;
        }

        if (moods?.Length > 0 && !HasMood(audio))
        {
            if ((extension == ".flac" || extension == ".ogg" || extension == ".opus") && audio.GetTag(TagTypes.Xiph, true) is TagLib.Ogg.XiphComment moodXiph)
            {
                moodXiph.SetField("MOOD", moods);
                changed = true;
            }
            else if (extension == ".mp3" && audio.GetTag(TagTypes.Id3v2, true) is TagLib.Id3v2.Tag moodId3)
            {
                UserTextInformationFrame.Get(moodId3, "MOOD", true).Text = moods;
                changed = true;
            }
            else if ((extension == ".m4a" || extension == ".mp4") && audio.GetTag(TagTypes.Apple, true) is TagLib.Mpeg4.AppleTag moodApple)
            {
                moodApple.SetDashBox("com.apple.iTunes", "MOOD", string.Join("; ", moods));
                changed = true;
            }
        }

        if (changed) audio.Save();
        return changed;
    }

    public static bool HasMood(TagLib.File audio)
    {
        if (audio.GetTag(TagTypes.Xiph) is TagLib.Ogg.XiphComment xiph &&
            (xiph.GetField("MOOD").Length > 0 || xiph.GetField("ALBUMMOOD").Length > 0)) return true;
        if (audio.GetTag(TagTypes.Id3v2) is TagLib.Id3v2.Tag id3 &&
            (id3.GetFrames(ByteVector.FromString("TMOO", StringType.Latin1)).Any() || id3.GetFrames<UserTextInformationFrame>()
            .Any(frame => string.Equals(frame.Description, "MOOD", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(frame.Description, "ALBUMMOOD", StringComparison.OrdinalIgnoreCase)))) return true;
        if (audio.GetTag(TagTypes.Apple) is TagLib.Mpeg4.AppleTag apple && apple.GetDashBox("com.apple.iTunes", "MOOD") != null) return true;
        return false;
    }
}
