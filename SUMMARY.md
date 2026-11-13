# Lidarr Content Filter Plugin

A comprehensive Lidarr plugin for filtering remixes, edits, live versions, acoustic versions, and other alternate versions of tracks. Keep only the clean/original versions you want.

## Overview

This plugin is similar to Tubifarry's QueueCleaner but focuses specifically on content filtering - identifying and managing different versions of tracks. It automatically detects:

- **Remixes**: Remixes, edits, bootlegs, reinterpretations, reworks
- **Edits**: Radio edits, album edits, single edits, extended versions
- **Live Versions**: Live recordings vs studio versions
- **Acoustic Versions**: Acoustic versions vs original recordings
- **Demo Versions**: Demo versions vs final releases
- **Other Versions**: Version markers, alternative versions, etc.

## Features

- **Smart Detection**: Uses pattern matching and duration comparison to identify versions
- **Configurable**: Customize which release types and version types to filter
- **Flexible Actions**: Choose to flag for review, unmonitor, or skip detected versions
- **Duration Filtering**: Set minimum/maximum track durations to ignore outliers
- **Multi-Track Protection**: Mixed content is flagged for review, not auto-deleted

## Installation

1. Build the plugin: `dotnet build src/ContentFilter/ContentFilter.csproj`
2. Copy the DLL to Lidarr's Plugins folder
3. Restart Lidarr
4. Configure in Settings → Connect

## Usage

1. Add a Content Filter connection in Settings → Connect
2. Configure your preferences (which release types, which version types to detect)
3. The plugin will automatically run when albums are imported
4. Review flagged items in Lidarr's log

## Configuration

See README.md for detailed configuration options.

## Development

See DEVELOPMENT.md for build instructions and version detection logic details.

## License

MIT License - See LICENSE file for details.

## Acknowledgments

- Inspired by [Tubifarry's QueueCleaner](https://github.com/Tubifarry/Lidarr.Plugins.QueueCleaner)
- Based on the Unique Singles plugin structure
