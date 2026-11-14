# Music Enhancer Plugin v1.0.1.4

## What's New

### Connect Provider
- The plugin now appears under **Settings → Connect** as **Music Enhancer**
- Add it like any other connection to access all plugin settings
- Content filtering runs automatically after an album import

### Safer Filtering
- **Flag for review** (default) only logs detected versions and never deletes files
- **Skip** leaves the album untouched
- Files are deleted only when **Delete files after unmonitor** is enabled
- Release type and track duration settings are now applied

### Compatibility Fix
- Fixed the plugin load failure on Lidarr 3.1.x hosts
  (`Could not load file or assembly 'Lidarr.Core, Version=3.2.1.5196'`)
- Compiles against Lidarr `3.1.6.5079` reference assemblies so it loads on 3.1.x and newer 3.2.x hosts
- Restored the plugin entry point so Lidarr detects and lists the plugin

### Build and Packaging
- Build against the verified prebuilt Lidarr release assemblies
- Release assets include the `net8.0.zip` package required by Lidarr's plugin installer
- Fixed StyleCop `SA1200` build failures
- Removed the Lidarr git submodule; the build script downloads reference assemblies automatically
- Clean build with zero warnings

## Previous Release: v1.0.0.0

### Version Filtering
- Detect and filter remixes, edits, live versions, acoustic versions, and demos
- Track-level unmonitoring - unmonitor individual tracks within an album
- Configurable version filter action (Flag for review, Unmonitor, Skip)

### Explicit Tag Support
- Automatically detect and tag explicit content for Symfonium/Jellyfin compatibility
- Supports MP4, FLAC, MP3, OGG, and Opus formats
- Works alongside content filtering as a separate feature

### Metadata Enrichment
- Enrich metadata from multiple sources:
  - **Last.fm** - Play counts, ratings, tags, genres
  - **Deezer** - Duration, preview, album info
  - **Discogs** - Release date, label, catalog number
  - **AcoustID** - MusicBrainz Recording ID, duration accuracy
  - **MusicBrainz** - Recording ID, score, release information

### Configuration Options
- Version filter action
- Release types to filter (Album, EP, Single, Compilation)
- Duration tolerance
- Per-version-type enable/disable toggles
- Minimum/maximum track duration
- Delete files after unmonitor (default: true)
- Enable explicit tagging (default: true)
- Enable metadata writing (default: true)
- API keys for Last.fm, Deezer, Discogs, AcoustID

## Installation

1. Build the plugin: `dotnet build src/MusicEnhancer/MusicEnhancer.csproj`
2. Copy the DLL to Lidarr's Plugins folder
3. Restart Lidarr
4. Configure in Settings → Connect

## Requirements

- Lidarr `3.1.6` or newer running on .NET 8
- .NET 8.0 SDK (for building)

## License

MIT License - See [LICENSE](LICENSE) file

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for detailed changelog.
