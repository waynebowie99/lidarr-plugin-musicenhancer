using System;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

internal static class MusicFileIdentity
{
    public static bool IsWithinArtistFolder(Artist artist, string filePath)
    {
        if (string.IsNullOrWhiteSpace(artist.Path)) return false;
        var root = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(artist.Path));
        var current = System.IO.Path.GetFullPath(filePath);
        var relativePath = System.IO.Path.GetRelativePath(root, current);
        if (System.IO.Path.IsPathRooted(relativePath) || relativePath == ".." ||
            relativePath.StartsWith(".." + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            relativePath.StartsWith(".." + System.IO.Path.AltDirectorySeparatorChar, StringComparison.Ordinal)) return false;

        // A lexical prefix alone does not prevent a symlink inside the artist folder
        // from pointing outside it. Skip links rather than following them for writes.
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        while (true)
        {
            if ((System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0) return false;
            if (string.Equals(current, root, comparison)) return true;
            var parent = System.IO.Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, comparison)) return false;
            current = parent;
        }
    }

    /// <summary>Reject an explicit recording-ID conflict; missing IDs remain inconclusive.</summary>
    public static bool HasNoRecordingIdConflict(string path, Track track)
    {
        if (!Guid.TryParse(track.ForeignRecordingId, out var expected)) return true;
        using var audio = TagLib.File.Create(path);
        return !Guid.TryParse(audio.Tag.MusicBrainzTrackId, out var existing) || existing == expected;
    }
}
