using System;
using System.Linq;
using TagLib;
using TagLib.Id3v2;
using TagLib.Mpeg4;
using TagLib.Ogg;

namespace NzbDrone.Core.Plugins;

/// <summary>Fill-only format-specific fields, checking recognized aliases before writing.</summary>
internal sealed class SymfoniumFieldWriter
{
    private readonly TagLib.File _file;
    private readonly string _extension;

    public SymfoniumFieldWriter(TagLib.File file, string extension)
    {
        _file = file;
        _extension = extension;
    }

    public bool Write(string? value, string[] aliases, string? id3Frame = null, byte[]? appleAtom = null, string? id3Alias = null, string? id3Fallback = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        if (Exists(aliases, id3Frame, appleAtom, id3Fallback)) return false;

        if (_extension is ".flac" or ".ogg" or ".opus")
        {
            if (_file.GetTag(TagTypes.Xiph, true) is not XiphComment writable) return false;
            writable.SetField(aliases[0], value);
            return true;
        }
        if (_extension == ".mp3")
        {
            if (_file.GetTag(TagTypes.Id3v2, true) is not TagLib.Id3v2.Tag writable) return false;
            if (id3Frame != null) writable.SetTextFrame(Id(writable.Version < 4 && id3Fallback != null ? id3Fallback : id3Frame), new[] { value });
            else UserTextInformationFrame.Get(writable, id3Alias ?? aliases[0], true).Text = new[] { value };
            return true;
        }
        if (_extension is ".m4a" or ".mp4")
        {
            if (_file.GetTag(TagTypes.Apple, true) is not AppleTag writable) return false;
            // Apple free-form tags are documented mappings for the supported fields.
            writable.SetDashBox("com.apple.iTunes", aliases[0], value);
            return true;
        }
        return false;
    }

    public bool Exists(string[] aliases, string? id3Frame = null, byte[]? appleAtom = null, string? id3Fallback = null)
    {
        // Check all tag types that may coexist in one file, not just the preferred type.
        if (_file.GetTag(TagTypes.Xiph) is XiphComment xiph && aliases.Any(name => xiph.GetField(name).Length > 0)) return true;
        if (_file.GetTag(TagTypes.Id3v2) is TagLib.Id3v2.Tag id3)
        {
            if (id3Frame != null && id3.GetFrames(Id(id3Frame)).Any()) return true;
            if (id3Fallback != null && id3.GetFrames(Id(id3Fallback)).Any()) return true;
            if (id3.GetFrames<UserTextInformationFrame>().Any(frame => aliases.Any(name =>
                string.Equals(frame.Description, name, StringComparison.OrdinalIgnoreCase)))) return true;
        }
        if (_file.GetTag(TagTypes.Apple) is AppleTag apple)
        {
            if (appleAtom != null && apple.DataBoxes(new ByteVector(appleAtom)).Any()) return true;
            if (AppleTagHelpers.HasFreeform(apple, aliases)) return true;
        }
        return false;
    }

    public bool WriteCompilation()
    {
        var id = Id("TCMP");
        var appleId = Id("cpil");
        if (_file.GetTag(TagTypes.Xiph) is XiphComment xiph && xiph.GetField("COMPILATION").Length > 0) return false;
        if (_file.GetTag(TagTypes.Id3v2) is TagLib.Id3v2.Tag id3 &&
            (id3.GetFrames(id).Any() || id3.GetFrames<UserTextInformationFrame>().Any(frame =>
                string.Equals(frame.Description, "COMPILATION", StringComparison.OrdinalIgnoreCase)))) return false;
        if (_file.GetTag(TagTypes.Apple) is AppleTag apple &&
            (apple.DataBoxes(appleId).Any() || AppleTagHelpers.HasFreeform(apple, "COMPILATION"))) return false;

        if (_extension is ".flac" or ".ogg" or ".opus")
        {
            if (_file.GetTag(TagTypes.Xiph, true) is not XiphComment writable) return false;
            writable.SetField("COMPILATION", "1");
        }
        else if (_extension == ".mp3")
        {
            if (_file.GetTag(TagTypes.Id3v2, true) is not TagLib.Id3v2.Tag writable) return false;
            writable.SetTextFrame(id, new[] { "1" });
        }
        else if (_extension is ".m4a" or ".mp4")
        {
            if (_file.GetTag(TagTypes.Apple, true) is not AppleTag writable) return false;
            writable.IsCompilation = true;
        }
        else return false;
        return true;
    }

    private static ByteVector Id(string value) => ByteVector.FromString(value, StringType.Latin1);
}
