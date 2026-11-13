# Lidarr Music Enhancer Plugin - Complete Summary

## What This Plugin Does

The **Music Enhancer** plugin is a comprehensive Lidarr plugin for filtering remixes, edits, live versions, acoustic versions, and other alternate versions of tracks. It helps you keep only the clean/original versions you want while automatically managing versions you don't care about.

## Key Feature: Track-Level Unmonitoring

**The plugin now supports unmonitoring individual tracks within an album, not just entire albums!**

This is perfect for albums that contain a mix of original tracks and versions:
- "Greatest Hits" with both original songs and remixes
- Deluxe editions with bonus tracks and alternate versions
- Compilations with mixed content

### How Track-Level Unmonitoring Works:

**Scenario: Mixed Album**
- Track 1: "Song Name" (original) → **Remains monitored**
- Track 2: "Song Name (Acoustic)" (version) → **Unmonitored**
- Track 3: "Another Song" (original) → **Remains monitored**
- Track 4: "Third Song (Live)" (version) → **Unmonitored**

Result: Only the 2 version tracks are unmonitored; the 2 original tracks remain in your library

## Key Feature: Explicit Tag Support

**Automatically detect and tag explicit content for Symfonium/Jellyfin compatibility!**

The metadata enhancement service:
- Reads track titles to detect explicit content indicators
- Supports multiple file formats: MP4, FLAC, MP3, OGG, Opus
- Writes explicit tags to actual media files
- Works alongside content filtering - separate feature!

### Supported File Formats:
- **MP4/M4A/M4B**: Uses iTunes metadata (ITUNESADVISORY)
- **FLAC**: Uses Vorbis comments (EXPLICIT tag)
- **MP3**: Uses ID3 tags (TXXX frame)
- **OGG/OGA**: Uses Vorbis comments
- **Opus**: Uses Vorbis comments

### How Explicit Tagging Works:
- Tracks with "explicit" in title → Marked as explicit
- Tracks with "clean", "edited", "radio edit" → NOT marked as explicit
- Explicit tags written to actual media files for Symfonium/Jellyfin

## Key Feature: File Deletion Control

**The plugin now includes a configurable option to control file deletion after unmonitoring:**

- **Delete files after unmonitor (default: ON)** - Files are automatically deleted after unmonitoring a version
- **Delete files after unmonitor (OFF)** - Only the track/album is unmonitored, files remain on disk

This gives you full control over whether you want to:
1. **Clean up completely** (delete files + unmonitor) - Default behavior
2. **Just unmonitor** (keep files but stop tracking) - For archival purposes

## Key Feature: Explicit Tag Support

**Automatically detect and tag explicit content for Symfonium/Jellyfin compatibility!**

The metadata enhancement service:
- Reads track titles to detect explicit content indicators
- Supports multiple file formats: MP4, FLAC, MP3, OGG, Opus
- Writes explicit tags to actual media files
- Works alongside content filtering - separate feature!

### Supported File Formats:
- **MP4/M4A/M4B**: Uses iTunes metadata (ITUNESADVISORY)
- **FLAC**: Uses Vorbis comments (EXPLICIT tag)
- **MP3**: Uses ID3 tags (TXXX frame)
- **OGG/OGA**: Uses Vorbis comments
- **Opus**: Uses Vorbis comments

### How Explicit Tagging Works:
- Tracks with "explicit" in title → Marked as explicit
- Tracks with "clean", "edited", "radio edit" → NOT marked as explicit
- Explicit tags written to actual media files for Symfonium/Jellyfin

## Features

1. **Version Detection**: Automatically identifies different version types:
   - Remixes (remix, edit, bootleg, reinterpretation, etc.)
   - Edits (radio edit, album edit, extended version, etc.)
   - Live versions (live, live at, unplugged, etc.)
   - Acoustic versions (acoustic, unplugged, etc.)
   - Demo versions (demo, demo version, etc.)
   - Other versions (version, alternative version, etc.)

2. **Smart Matching**: Uses pattern matching and duration comparison to identify versions with high accuracy

3. **Track-Level Unmonitoring**: Unmonitor individual tracks within an album (not just entire albums!)

4. **Explicit Tag Support**: Automatically detect and tag explicit content for Symfonium/Jellyfin compatibility

5. **Configurable**: Customize which release types and version types to filter

6. **Flexible Actions**: Choose to flag for review, unmonitor, or skip detected versions

7. **Multi-Track Protection**: Mixed content is flagged for review, not auto-deleted

8. **File Deletion Control**: Configurable option to delete files after unmonitoring (default: true)

## Configuration Options

The plugin provides 11+ configurable settings:

1. **Version filter action** - Flag for review, Unmonitor, or Skip
2. **Release types to filter** - Album, EP, Single, Compilation
3. **Duration tolerance** - Maximum duration difference for matching (default: 3000ms)
4. **Enable remix filtering** - Filter remixes, edits, bootlegs
5. **Enable version filtering** - Filter version markers
6. **Enable live version filtering** - Filter live versions
7. **Enable acoustic filtering** - Filter acoustic versions
8. **Enable edit filtering** - Filter edit versions
9. **Enable demo filtering** - Filter demo versions
10. **Minimum track duration** - Minimum track duration in milliseconds (default: 30000ms)
11. **Maximum track duration** - Maximum track duration in milliseconds (default: 600000ms)
12. **Delete files after unmonitor** - Delete media files after unmonitoring (default: true)

## How It Works

1. When an album is imported, the plugin analyzes all tracks
2. Each track is checked against all configured version types
3. Version matches are identified using pattern matching and duration comparison
4. Decision logic determines the action:
   - **All tracks are versions** → Unmonitor entire album
   - **Mixed content** → Unmonitor only version tracks, keep originals
   - **No versions found** → Leave as-is
5. If unmonitoring is triggered:
   - **Album unmonitoring**: Album is unmonitored in Lidarr
   - **Track unmonitoring**: Individual tracks are unmonitored, originals remain
   - **Files are deleted** (if Delete files after unmonitor is ON)
   - **Files are kept** (if Delete files after unmonitor is OFF)

## Example Scenarios

### Scenario 1: Remix Single (with default settings)
- Track 1: "Song Name (Remix)" 
- Track 2: "Song Name (Extended Mix)"
- Track 3: "Song Name (Bootleg)"

Result: All tracks identified as versions → Release unmonitored and **files deleted**

### Scenario 2: Mixed Album (Track-Level Unmonitoring)
- Track 1: "Song Name" (original) - **Remains monitored**
- Track 2: "Song Name (Acoustic)" (version) - **Unmonitored**
- Track 3: "Another Song" (original) - **Remains monitored**
- Track 4: "Third Song (Live)" (version) - **Unmonitored**

Result: Only version tracks unmonitored; original tracks remain in library

### Scenario 3: Deluxe Edition
- Track 1-10: Original album tracks - **Remain monitored**
- Track 11-15: Bonus tracks (remixes/edits) - **Unmonitored**

Result: Original album remains, bonus versions are removed

### Scenario 4: Original Album
- Track 1: "Song Name"
- Track 2: "Another Song"
- Track 3: "Third Track"

Result: No versions found → Release left as-is

## Requirements

- Lidarr nightly build (plugin support not in stable release)
- .NET 8.0 SDK (for building)

## Installation

1. Build the plugin: `dotnet build src/MusicEnhancer/MusicEnhancer.csproj`
2. Copy the DLL to Lidarr's Plugins folder
3. Restart Lidarr
4. Configure in Settings → Connect

## License

MIT License - See LICENSE file for details.

## Files

See PROJECT_SUMMARY.md for complete file listing.

## Documentation

- **README.md** - User documentation
- **docs/QUICK_START.md** - Quick start guide
- **docs/VERSION_DETECTION.md** - Version detection details
- **DEVELOPMENT.md** - Development guide
