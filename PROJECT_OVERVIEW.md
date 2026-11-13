# Lidarr Music Enhancer Plugin

A comprehensive Lidarr plugin for filtering remixes, edits, live versions, acoustic versions, and other alternate versions of tracks. Keep only the clean/original versions you want.

## Project Structure

```
MusicEnhancer/
├── src/
│   └── MusicEnhancer/
│       ├── MusicEnhancerPlugin.cs     # Plugin entry point
│       ├── ContentFilterSettings.cs   # Configuration settings
│       ├── ContentFilterOptions.cs    # Runtime options
│       ├── ContentFilterService.cs    # Main filtering service
│       ├── VersionMatcher.cs          # Version detection logic
│       ├── TitleNormalizer.cs         # Title normalization
│       └── MusicEnhancer.csproj       # Project file
├── docs/
│   ├── VERSION_DETECTION.md           # Version detection documentation
│   └── QUICK_START.md                 # Quick start guide
├── README.md                          # User documentation
├── DEVELOPMENT.md                     # Development guide
├── PROJECT_SUMMARY.md                 # Project summary
├── PROJECT_OVERVIEW.md                # This file
├── CHANGELOG.md                       # Changelog
├── build.bat                          # Windows build script
├── build.sh                           # Linux/macOS build script
└── .gitignore                         # Git ignore file
```

## Features

- **Version Detection**: Identify remixes, edits, live versions, acoustic versions, demos
- **Smart Matching**: Pattern matching with duration comparison
- **Configurable**: Customize release types and version types
- **Flexible Actions**: Flag for review, unmonitor, or skip
- **Multi-Track Protection**: Mixed content flagged for review
- **File Deletion Control**: Configurable option to delete files after unmonitoring (default: true)
- **Track-Level Unmonitoring**: Unmonitor individual tracks within an album (not just entire albums!)
- **Explicit Tag Support**: Automatically detect and tag explicit content for Symfonium/Jellyfin compatibility (MP4, FLAC, MP3, OGG, Opus)

## Version Detection

The plugin uses pattern matching to identify 6 version types:

1. **Remix** (confidence: 0.95) - remix, edit, bootleg, reinterpretation
2. **Edit** (confidence: 0.90) - radio edit, album edit, extended
3. **Version** (confidence: 0.85) - version, alternative version
4. **Live** (confidence: 0.80) - live, live at, unplugged
5. **Acoustic** (confidence: 0.80) - acoustic, unplugged
6. **Demo** (confidence: 0.75) - demo, demo version

## Configuration

Key settings:
- Version filter action (Flag for review, Unmonitor, Skip)
- Release types to filter (Album, EP, Single, Compilation)
- Duration tolerance (default: 3000ms)
- Enable/disable version type filters
- Minimum/maximum track duration
- **Delete files after unmonitor** (default: true)

## Requirements

- Lidarr nightly build
- .NET 8.0 SDK (for building)

## Installation

1. Build the plugin: `dotnet build src/MusicEnhancer/MusicEnhancer.csproj`
2. Copy the DLL to Lidarr's Plugins folder
3. Restart Lidarr
4. Configure in Settings → Connect

## Documentation

- **[README.md](README.md)** - User documentation
- **[docs/QUICK_START.md](docs/QUICK_START.md)** - Quick start guide
- **[docs/VERSION_DETECTION.md](docs/VERSION_DETECTION.md)** - Version detection details
- **[DEVELOPMENT.md](DEVELOPMENT.md)** - Development guide

## License

MIT License - See [LICENSE](LICENSE) file
