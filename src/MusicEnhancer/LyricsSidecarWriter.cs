using System;
using System.IO;
using System.Linq;
using System.Text;
using TagLib;
using TagLib.Id3v2;
using TagLib.Mpeg4;
using TagLib.Ogg;

namespace NzbDrone.Core.Plugins;

public static class LyricsSidecarWriter
{
    public static bool HasLyrics(string path)
    {
        var sidecar = Path.ChangeExtension(path, ".lrc");
        if (System.IO.File.Exists(sidecar)) return true;

        using var audio = TagLib.File.Create(path);
        if (!string.IsNullOrWhiteSpace(audio.Tag.Lyrics)) return true;
        if (audio.GetTag(TagTypes.Id3v2) is TagLib.Id3v2.Tag id3 && id3.GetFrames<SynchronisedLyricsFrame>().Any()) return true;
        if (audio.GetTag(TagTypes.Xiph) is XiphComment xiph &&
            new[] { "LYRICS", "SYNCEDLYRICS", "UNSYNCEDLYRICS" }.Any(key => xiph.GetField(key).Length > 0)) return true;
        if (audio.GetTag(TagTypes.Apple) is AppleTag apple && AppleTagHelpers.HasFreeform(apple, "LYRICS", "SYNCEDLYRICS", "UNSYNCEDLYRICS")) return true;
        return false;
    }

    public static bool WriteIfMissing(string path, string lyrics, Action? beforeSave = null)
    {
        if (string.IsNullOrWhiteSpace(lyrics) || HasLyrics(path)) return false;
        try
        {
            beforeSave?.Invoke();
            using var stream = new FileStream(Path.ChangeExtension(path, ".lrc"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(lyrics);
            return true;
        }
        catch (IOException) when (System.IO.File.Exists(Path.ChangeExtension(path, ".lrc")))
        {
            return false;
        }
    }
}
