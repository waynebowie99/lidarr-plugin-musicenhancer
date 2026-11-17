# Music Enhancer v1.0.2.1

## Metadata lookup performance

- Tracks selected by the configured version filter are excluded before metadata lookup.
- Existing field values are checked before online queries.
- Independent services run concurrently with a bounded worker pool; MusicBrainz remains rate-limited.
- MusicBrainz recording data is reused for metadata, composer, ISRC and explicit-status decisions.

# Music Enhancer v1.0.2.0

## Safety fixes

- Never delete unmatched files when a majority of an album matches. Shared files, symlinks, paths outside the artist folder and recording-ID conflicts are skipped.
- Unmonitor only when every known track in the album is downloaded and matches. Lidarr does not support individual track monitoring.
- File deletion defaults off for **new** connections; saved settings on existing connections remain unchanged. Review those settings after updating.
- Conservative title matching avoids common substring false positives and does not treat remasters as edits. Short commentary tracks are no longer excluded by duration limits.
- Existing clean advisory tags are honored even if Lidarr's track title does not say “clean.”
- Require a matching version-marked file title or MusicBrainz recording ID before destructive actions; ambiguous files remain untouched.
- Invalid filter actions are non-destructive. Startup scans skip conflicting multiple configurations and continue past individual album errors.

## Metadata and cleanup

- Additional fill-only Lidarr mappings: medium, disc subtitle, compilation, country, edition, original year, release track ID and per-disc track count.
- Optional MusicBrainz recording lookup for credited artists, linked work, public community track tags and recognizable moods. Off by default.
- More robust preservation of differently cased MP4 freeform tags and embedded recording-ID conflicts.
- Removed dead settings classes and unused dependencies. Legacy unused API-key settings remain available so saved values are not silently discarded.

See the [README](README.md) for setup, source availability, Jellyfin limitations and remaining unsupported fields.
