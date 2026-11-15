# Changelog

All notable changes to the Lidarr Music Enhancer plugin will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1.8] - 2026-11-14

### Added
- Opt-in explicit and clean audio-file advisory tagging using Lidarr track/file associations.
- Optional key-free MusicBrainz recording and Deezer ISRC lookup; unknown or conflicting ratings are left untouched.
- Startup scans now run enabled metadata processing as well as filtering.
- Round-trip tests for MP3, FLAC, OGG, Opus and M4A tags.

### Safety
- Existing advisory tags and shared files are not changed; file size and modification time are updated in Lidarr after writing.
- New metadata processing and online lookup switches default to off, including for existing connections.

## [1.0.1.7] - 2026-11-14

### Added
- Commentary track detection (`Commentary`, `Album Commentary`, `Track Commentary`, etc.)
- "Enable commentary filtering" setting (on by default)

## [1.0.1.6] - 2026-11-14

### Added
- "Keep clean versions" setting (on by default) so `Clean`, `Clean Version`, `Clean Edit`, etc. are kept while other edits are still filtered
- Clean version detection is separate from edit/version detection

### Fixed
- Edits are now classified as edits instead of remixes
- Clean markers no longer cause a title to be treated as a generic version

## [1.0.1.5] - 2026-11-14

### Added
- "Scan library on startup" setting that runs the filter over existing monitored albums when Lidarr starts

## [1.0.1.4] - 2026-11-14

### Added
- Connect provider so the plugin appears under **Settings → Connect** with its full settings UI
- Content filtering now runs after an album import
- Release-type, duration range, and "delete files after unmonitor" settings are now honored

### Fixed
- "Flag for review" action no longer deletes or unmonitors files
- "Skip" action now skips processing instead of deleting
- Files are only deleted when "Delete files after unmonitor" is enabled

## [1.0.1.3] - 2026-11-14

### Fixed
- Plugin load failure (`Could not load file or assembly 'Lidarr.Core, Version=3.2.1.5196'`)
  on Lidarr 3.1.x hosts by targeting Lidarr `3.1.6.5079` reference assemblies
- Restored the `MusicEnhancerPlugin : Plugin` entry point so Lidarr discovers the plugin

### Changed
- Build against Lidarr `3.1.6.5079` references so the plugin loads on 3.1.x and newer 3.2.x hosts
- Release assets now include the `net8.0.zip` package required by Lidarr's plugin installer

## [1.0.1.2] - 2026-11-14

### Fixed
- Build failures caused by StyleCop `SA1200` errors when building Lidarr from source
- Nullable reference warnings in plugin settings and version matching

### Changed
- Build against the verified prebuilt Lidarr release assemblies instead of the Lidarr source tree
- Removed the Lidarr git submodule; `scripts/prepare-lidarr.py` downloads the reference assemblies
- Updated build scripts, CI workflow, and documentation for the new build process

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
- Python 3 (to download Lidarr reference assemblies)
