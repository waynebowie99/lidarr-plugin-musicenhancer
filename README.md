# Music Enhancer

A Lidarr plugin that automatically identifies and filters remixes, edits, live versions, acoustic versions, and other alternate versions of tracks. Keep only the clean/original versions you want while unmonitoring versions you don't care about.

Also includes **metadata enhancement** for explicit tag support - automatically detects and tags explicit content for Symfonium/Jellyfin compatibility.

## Features

- **Remix Detection**: Automatically identify remixes, edits, bootlegs, reinterpretations, and reworks
- **Edit Detection**: Find radio edits, album edits, single edits, and other edited versions
- **Live Version Detection**: Identify live recordings vs studio versions
- **Acoustic Version Detection**: Find acoustic versions vs original recordings
- **Demo Detection**: Identify demo versions vs final releases
- **Version Filtering**: Detect version markers like "Version", "Alternative Version", etc.
- **Track-Level Unmonitoring**: Unmonitor individual tracks within an album (not just entire albums)
- **Explicit Tag Support**: Automatically detect and tag explicit content for Symfonium/Jellyfin compatibility (MP4, FLAC, MP3, OGG, Opus)
- **Configurable**: Customize which release types to filter and which version types to detect
- **Flexible Actions**: Choose to flag for review, unmonitor, or skip detected versions

## Requirements

- **Lidarr nightly branch** — plugin support is not available in the stable release channel.

## Installation

1. Open Lidarr and go to **System → Plugins** (or **Settings → General → Plugins** on some builds)

2. Paste the repository URL:

```
https://github.com/waynebowie99/lidarr-plugin-musicenhancer
```

3. Click **Install**

4. Lidarr will download the plugin and restart

## Configuration

After installation, add a Music Enhancer connection:

1. Go to **Settings → Connect**
2. Click the **+** button to add a new connection
3. Select **Music Enhancer** from the list
4. Configure the settings (see below) and click **Save**

### Settings

| Setting | Default | Description |
|---------|---------|-------------|
| **Version filter action** | Flag for review | What to do when versions are detected. Options: Flag for review, Unmonitor, Skip |
| **Release types to filter** | `Album, EP` | Comma-separated release types to apply filtering to |
| **Duration tolerance (ms)** | 3000 | Maximum duration difference for matching (±3 seconds) |
| **Enable remix filtering** | On | Filter remixes, edits, bootlegs, reinterpretations |
| **Enable version filtering** | On | Filter version markers |
| **Enable live version filtering** | On | Filter live versions |
| **Enable acoustic filtering** | On | Filter acoustic versions |
| **Enable edit filtering** | On | Filter edit versions |
| **Enable demo filtering** | On | Filter demo versions |
| **Minimum track duration (ms)** | 30000 | Minimum track duration in milliseconds (30 seconds) |
| **Maximum track duration (ms)** | 600000 | Maximum track duration in milliseconds (10 minutes) |
| **Delete files after unmonitor** | On | Delete media files after unmonitoring a version. Defaults to true (delete files). |

## How It Works

When you import an album or EP, Music Enhancer analyzes all tracks to identify versions:

1. **Remix Detection**: Looks for remix, edit, bootleg, reinterpretation, rework, remake, etc. in track titles
2. **Edit Detection**: Identifies radio edits, album edits, single edits, extended versions, etc.
3. **Live Version Detection**: Finds live recordings vs studio versions
4. **Acoustic Detection**: Identifies acoustic versions vs original recordings
5. **Demo Detection**: Finds demo versions vs final releases
6. **Version Detection**: Identifies version markers like "Version", "Alternative Version", etc.

### Track-Level Unmonitoring

**Key Feature**: Music Enhancer can now unmonitor individual tracks within an album, not just entire albums!

When a mixed album contains both original tracks and versions:
- **Original tracks**: Remain monitored and downloaded
- **Version tracks**: Are unmonitored and optionally deleted

This is perfect for albums like:
- "Greatest Hits" with both original songs and remixes
- Deluxe editions with bonus tracks and alternate versions
- Compilations with mixed content

### Decision Logic

- **All tracks are versions**: The entire release is unmonitored and files are deleted (unless disabled in settings)
- **Mixed content (some originals + some versions)**: Only version tracks are unmonitored, originals remain
- **No versions found**: Release is left as-is

The **Delete files after unmonitor** setting controls whether media files are deleted after unmonitoring:
- **Enabled (default)**: Files are deleted after unmonitoring
- **Disabled**: Only the track/album is unmonitored, files remain on disk

## Example Scenarios

**Scenario 1: Remix Single**
- Track 1: "Song Name (Remix)" 
- Track 2: "Song Name (Extended Mix)"
- Track 3: "Song Name (Bootleg)"

Result: All tracks identified as versions → Release unmonitored and files deleted (unless disabled in settings)

**Scenario 2: Mixed Album with Originals + Versions**
- Track 1: "Song Name" (original) - **Remains monitored**
- Track 2: "Song Name (Acoustic)" (version) - **Unmonitored**
- Track 3: "Another Song" (original) - **Remains monitored**
- Track 4: "Third Song (Live)" (version) - **Unmonitored**

Result: Only the 2 version tracks are unmonitored; the 2 original tracks remain in your library

**Scenario 3: Deluxe Edition**
- Track 1-10: Original album tracks - **Remain monitored**
- Track 11-15: Bonus tracks (remixes/edits) - **Unmonitored**

Result: Original album remains, bonus versions are removed

**Scenario 4: Original Album**
- Track 1: "Song Name"
- Track 2: "Another Song"
- Track 3: "Third Track"

Result: No versions found → Release left as-is

## Known Issues

- **Requires Lidarr nightly branch.** Plugin support is not available in the stable release channel.

## Related

- [Tubifarry's QueueCleaner](https://github.com/Tubifarry/Lidarr.Plugins.QueueCleaner) - Similar plugin for queue management
- [Unique Singles](https://github.com/jtstothard/lidarr-plugin-uniquesingles) - Plugin for removing duplicate singles

## Support

If you find this plugin useful, consider supporting the development.

## License

MIT
