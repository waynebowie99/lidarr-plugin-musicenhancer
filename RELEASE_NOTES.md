# Music Enhancer Plugin v1.0.0.0

## What's New

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

- Lidarr nightly build
- .NET 8.0 SDK (for building)

## License

MIT License - See [LICENSE](LICENSE) file

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for detailed changelog.
