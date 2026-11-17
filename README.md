# Music Enhancer

A Lidarr plugin that detects selected alternate versions and optionally fills missing audio metadata. It supports Lidarr's plugin framework and runs after release imports or, if enabled, on startup.

## Installation

1. In Lidarr, open **System → Plugins** and install `https://github.com/waynebowie99/lidarr-plugin-musicenhancer`.
2. Restart Lidarr, then add **Music Enhancer** in **Settings → Connect**.
3. Check **On Release Import** to process newly imported releases. Optionally enable **Scan library on startup** and restart to process existing monitored artists and albums.

Lidarr must support plugins. The plugin targets .NET 8 and is built against Lidarr 3.1.6.5079; it has also been smoke-tested against 3.2.1.5196. For a manual install, extract the release ZIP into `/config/plugins/waynebowie99/lidarr-plugin-musicenhancer/` (or the equivalent app-data directory), then restart Lidarr.

## Version filtering and file safety

**Version filter action** defaults to **Flag for review**: matching tracks are written to Lidarr's log and are not deleted or unmonitored. **Skip** disables version filtering. Metadata processing, if enabled, is independent of this action.

With **Unmonitor**:

- An album is unmonitored only if **every known track is downloaded and matches** an enabled filter. Albums with unmatched or undownloaded tracks remain monitored.
- Lidarr does **not** support monitoring individual tracks. Mixed albums remain monitored, so Lidarr may download a removed track again.
- **Delete matched track files** is off by default for new connections. If enabled, only matched, unshared files are deleted via Lidarr's deletion service; unmatched files are never deleted. Existing connections retain their previous saved deletion setting, so check it after updating.
- Before an unmonitor or delete action, the audio file must be inside the artist folder without a symlink in that path and have either the same version-marked title as Lidarr (ignoring case and surrounding whitespace) or the same MusicBrainz recording ID. Shared files, missing files and conflicting or unverified recordings are skipped. This conservative check can leave some real versions for manual review.

Back up your library and review the logs before enabling deletion. Deletion may be permanent if Lidarr's recycle bin is not configured. Detection is based on title markers, not audio analysis, and can miss or misclassify unusual titles.

By default, the plugin examines albums and EPs. **Release types to filter** accepts comma-separated `Album`, `EP`, `Single` and `Compilation`. Short tracks are ignored according to the duration settings, except commentary tracks, which are often short.

Examples include `Song (Remix)`, `Song (Radio Edit)`, `Song (Live at Wembley)`, `Song (Acoustic)`, `Song (Demo)` and `Album Commentary`. Ordinary titles such as `Alive`, `Credit` and `Song (Remastered 2011)` are not treated as versions. **Keep clean versions** defaults on: clean title markers and embedded clean advisory tags are preserved unless commentary is separately identified. If a candidate file's advisory cannot be inspected, it is skipped while this protection is enabled.

## Metadata

All changes require **Enable metadata processing** and **Enable metadata writing**. New or previously unset opt-ins default off; previously enabled settings remain unchanged. **Fill missing library tags** enables the Lidarr metadata fields listed below. Existing values are preserved; unknown or ambiguous data is left untouched. Supported formats are MP3, FLAC, OGG, Opus, M4A and MP4. Files outside the artist folder, reached through a symlink within it, mapped to multiple Lidarr tracks or with a conflicting embedded recording ID are skipped.

Back up files before enabling metadata writes. If a file is hard-linked elsewhere, modifying its tags may also affect the other link.

| Source | Available fields | Setting |
| --- | --- | --- |
| Lidarr | Title, artist, album and album artist, artist sort where known, genres, track/disc numbers and counts, year, label, release and recording IDs, release type/status, country, media type, disc subtitle, edition, original release year and compilation | Fill missing library tags |
| MusicBrainz | Recording artist credits, single linked work, public community track tags and recognized moods | Look up recording metadata online |
| MusicBrainz | Composer credits explicitly identified on linked works | Look up composers online |
| MusicBrainz and Deezer | Explicit/clean advisory; BPM via ISRC and duration checks | Look up explicit status online; Look up BPM online |
| Last.fm | A small set of recognizable community mood labels | Look up mood online; requires a Last.fm API key |
| LRCLIB | Matching synced or plain lyrics as a new `.lrc` sidecar | Look up lyrics online |

The optional online lookups send recording IDs, ISRCs or track details to the named services. Filtered tracks are excluded before lookups, and existing populated fields are checked first. Independent lookups run concurrently with a small worker limit; MusicBrainz remains limited to one request per second. Except for Last.fm mood lookup, no API key is required. Lyrics require matching artist, album, title and duration; existing embedded lyrics and `.lrc` files are preserved. MusicBrainz community tags are public user-submitted labels, not personal ratings. Existing legacy API-key settings are retained for compatibility but are unused.

**Explicit tagging** uses Lidarr's positive explicit flag, clean-title markers and optional online lookups. A false Lidarr flag or a Deezer “not explicit” result does not prove a track is a clean edit. MP3/FLAC/OGG/Opus use `ITUNESADVISORY=1` for explicit and `2` for clean; MP4 uses `rtng=4` and `2` respectively. Unknown ratings are not written.

Symfonium supports more fields than these sources expose. Personal ratings, favorites, comments and other user-defined values are never invented or changed. Style, occasion, track language, movement details and many other fields are not automatically populated without a reliable source.

### Jellyfin and Symfonium

After processing, refresh the Jellyfin music library and sync the provider in Symfonium. Symfonium's [provider feature matrix](https://docs.symfonium.app/wiki/providers/supported-features-by-media-providers/) lists Jellyfin support for multiple artists and genres, but not dedicated composer, BPM, album mood or style fields. Writing a file tag alone does not guarantee it appears through Jellyfin.

The third-party [Jellyfin MusicTags plugin](https://github.com/jyourstone/jellyfin-musictags-plugin) can expose selected file fields as **generic Jellyfin tags**, which are not necessarily dedicated Symfonium fields. For example:

```text
GENRE,MOOD,BPM,COMPOSER,STYLE,LANGUAGE,ITUNESADVISORY,ADVISORY,TRACK_TAGS,WORK,CONTENTGROUP,RELEASETYPE,RELEASESTATUS,RELEASECOUNTRY,COUNTRY,ALBUMVERSION,MEDIA,MEDIATYPE,DISCSUBTITLE,SETSUBTITLE,ORIGINALDATE,COMPILATION,LABEL,PUBLISHER,KEY
```

Set its value delimiter to `;` initially and leave overwrite/cleanup options disabled until you have reviewed the results. A separate SMB or WebDAV provider in Symfonium uses its own file-tag parser and may expose more fields. Partially populated MusicBrainz IDs can split albums in file providers; review Symfonium's parser settings if needed.

## Development

See [DEVELOPMENT.md](DEVELOPMENT.md) for building and testing. The plugin builds against verified, prebuilt Lidarr DLLs; it does not require the Lidarr source tree.
