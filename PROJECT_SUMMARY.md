# Lidarr Music Enhancer Plugin - Summary

## What This Plugin Does

The **Music Enhancer** plugin is a comprehensive Lidarr plugin for filtering remixes, edits, live versions, acoustic versions, and other alternate versions of tracks. It helps you keep only the clean/original versions you want while automatically managing versions you don't care about.

## Key Features

1. **Track-Level Unmonitoring**: Unmonitor individual tracks within an album (not just entire albums!)

2. **Explicit Tag Support**: Automatically detect and tag explicit content for Symfonium/Jellyfin compatibility (MP4, FLAC, MP3, OGG, Opus)

3. **Metadata Enrichment**: Enrich metadata from Last.fm, Deezer, Discogs, AcoustID, and MusicBrainz

4. **Version Detection**: Automatically identifies different version types:
   - Remixes (remix, edit, bootleg, reinterpretation, etc.)
   - Edits (radio edit, album edit, extended version, etc.)
   - Live versions (live, live at, unplugged, etc.)
   - Acoustic versions (acoustic, unplugged, etc.)
   - Demo versions (demo, demo version, etc.)
   - Other versions (version, alternative version, etc.)

3. **Smart Matching**: Uses pattern matching and duration comparison to identify versions with high accuracy

4. **Configurable**: Customize which release types and version types to filter

5. **Flexible Actions**: Choose to flag for review, unmonitor, or skip detected versions

6. **File Deletion Control**: Configurable option to delete files after unmonitoring (default: true)

## Project Structure

```
MusicEnhancer/
├── src/MusicEnhancer/
│   ├── MusicEnhancerPlugin.cs         # Plugin entry point
│   ├── ContentFilterSettings.cs       # Configuration settings
│   ├── ContentFilterOptions.cs        # Runtime options
│   ├── ContentFilterService.cs        # Main filtering service
│   ├── VersionMatcher.cs              # Version detection logic
│   ├── TitleNormalizer.cs             # Title normalization
│   └── MusicEnhancer.csproj           # Project file
├── docs/
│   ├── VERSION_DETECTION.md           # Version detection documentation
│   └── QUICK_START.md                 # Quick start guide
├── README.md                          # User documentation
├── DEVELOPMENT.md                     # Development guide
├── PROJECT_SUMMARY.md                 # This file
├── PROJECT_OVERVIEW.md                # Project structure overview
├── CHANGELOG.md                       # Changelog
├── build.bat                          # Windows build script
├── build.sh                           # Linux/macOS build script
└── .gitignore                         # Git ignore file
```

## Files Created

### Core Plugin Files
1. **MusicEnhancerPlugin.cs** - Plugin entry point with metadata
2. **ContentFilterSettings.cs** - Configuration with 12+ settings (including file deletion control)
3. **ContentFilterOptions.cs** - Runtime options for filtering
4. **ContentFilterService.cs** - Main service that orchestrates filtering with track-level unmonitoring
5. **MetadataEnhancerService.cs** - Metadata enhancement service for explicit tag support
6. **VersionMatcher.cs** - Core version detection logic with 6 detection types
7. **TitleNormalizer.cs** - Title normalization for matching

### Documentation Files
1. **README.md** - User-facing documentation
2. **DEVELOPMENT.md** - Development guide
3. **PROJECT_SUMMARY.md** - Project summary
4. **PROJECT_OVERVIEW.md** - Project structure overview
5. **CHANGELOG.md** - Version history
6. **docs/VERSION_DETECTION.md** - Detailed version detection documentation
7. **docs/QUICK_START.md** - Quick start guide

### Build Files
1. **MusicEnhancer.sln** - Visual Studio solution file
2. **build.bat** - Windows build script
3. **build.sh** - Linux/macOS build script
4. **.gitignore** - Git ignore file
5. **LICENSE** - MIT License

## Version Detection Logic

The plugin uses pattern matching to identify 6 version types:

1. **Remix Detection** (Confidence: 0.95)
   - Keywords: remix, edit, bootleg, reinterpretation, rework, remake, etc.
   
2. **Edit Detection** (Confidence: 0.90)
   - Keywords: edit, radio edit, album edit, extended, etc.
   
3. **Version Detection** (Confidence: 0.85)
   - Keywords: version, alternative version, alternate version, etc.
   
4. **Live Version Detection** (Confidence: 0.80)
   - Keywords: live, live at, unplugged, etc.
   
5. **Acoustic Detection** (Confidence: 0.80)
   - Keywords: acoustic, unplugged, etc.
   
6. **Demo Detection** (Confidence: 0.75)
   - Keywords: demo, demo version, demo recording, etc.

## Configuration Options

The plugin provides 12+ configurable settings:

1. Version filter action (Flag for review, Unmonitor, Skip)
2. Release types to filter (Album, EP, Single, Compilation)
3. Duration tolerance (default: 3000ms)
4. Enable/disable each version type filter
5. Minimum/maximum track duration
6. **Delete files after unmonitor** (default: true) - Controls whether media files are deleted after unmonitoring
7. **Enable explicit tagging** (default: true) - Automatically detect and tag explicit content
8. **Enable metadata writing** (default: true) - Write metadata tags to files

## How It Works

1. When an album is imported, the plugin analyzes all tracks
2. Each track is checked against all configured version types
3. Version matches are identified using pattern matching and duration comparison
4. Decision logic determines the action:
   - **All tracks are versions** → Unmonitor entire album
   - **Mixed content** → Unmonitor only version tracks, keep originals (NEW!)
   - **No versions found** → Leave as-is
5. If unmonitoring is triggered:
   - Track/album is unmonitored in Lidarr
   - **Files are deleted** (if Delete files after unmonitor is ON)
   - **Files are kept** (if Delete files after unmonitor is OFF)

## Requirements

- Lidarr nightly build
- .NET 8.0 SDK (for building)

## Installation

1. Build the plugin: `dotnet build src/MusicEnhancer/MusicEnhancer.csproj`
2. Copy the DLL to Lidarr's Plugins folder
3. Restart Lidarr
4. Configure in Settings → Connect

## License

MIT License - See LICENSE file for details.

## Similar Plugins

- **Tubifarry's QueueCleaner** - Similar plugin for queue management
- **Unique Singles** - Plugin for removing duplicate singles
