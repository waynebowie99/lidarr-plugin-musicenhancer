# Changelog

All notable changes to the Lidarr Music Enhancer plugin will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0.0] - 2026-11-13

### Added
- Initial release of the Music Enhancer plugin
- **Version Detection**: Remixes, edits, live versions, acoustic versions, demos
- **Track-Level Unmonitoring**: Unmonitor individual tracks within an album (not just entire albums!)
- **Explicit Tag Support**: Automatically detect and tag explicit content for Symfonium/Jellyfin compatibility
- **Metadata Enrichment**: Enrich metadata from Last.fm, Deezer, Discogs, AcoustID, and MusicBrainz
- **Configurable version filter action**: Flag for review, Unmonitor, or Skip
- **Configurable release types to filter**: Album, EP, Single, Compilation
- **Duration tolerance configuration**
- **Per-version-type enable/disable toggles**
- **Minimum/maximum track duration settings**
- **File deletion control**: Configurable option to delete files after unmonitoring (default: true)
- **Metadata enrichment API keys**: Last.fm, Deezer, Discogs, AcoustID configuration
- Multi-track protection (mixed content flagged for review)
- Comprehensive documentation

### Features
- Automatic version detection when albums are imported
- Pattern matching with duration comparison
- Title normalization for accurate matching
- Logging of all detection decisions
- Integration with Lidarr's unmonitor and delete functionality
- Track-level unmonitoring for mixed albums
- Flexible file deletion options
- Metadata enrichment from multiple sources

### Technical Details
- Built for Lidarr nightly branch
- Uses Lidarr's plugin framework
- Written in C# with .NET 8.0
- MIT licensed

## Future Releases

### Planned for [1.1.0.0]
- Scheduled library scanning
- Advanced pattern configuration
- Custom keyword support
- Statistics dashboard
- Import/export configuration

### Planned for [1.2.0.0]
- MusicBrainz metadata integration
- Fingerprint-based matching
- Machine learning-assisted detection
- Community pattern library
- Performance optimizations

## Known Issues

- Requires Lidarr nightly build
- Pattern matching may miss unusual version naming conventions
- Duration-based matching can have false positives for same-length tracks

## Dependencies

- Lidarr nightly build
- .NET 8.0 SDK
- Lidarr source code (for development)

