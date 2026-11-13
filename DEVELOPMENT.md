# Lidarr Music Enhancer Plugin

This is a Lidarr plugin for filtering remixes, edits, live versions, acoustic versions, and other alternate versions of tracks.

## Project Structure

```
MusicEnhancer/
├── src/
│   └── MusicEnhancer/
│       ├── MusicEnhancerPlugin.cs       # Plugin entry point
│       ├── ContentFilterSettings.cs     # Plugin configuration
│       ├── ContentFilterOptions.cs      # Runtime options
│       ├── ContentFilterService.cs      # Main filtering service
│       ├── VersionMatcher.cs            # Version detection logic
│       ├── TitleNormalizer.cs           # Title normalization
│       └── MusicEnhancer.csproj         # Project file
├── README.md                            # User documentation
└── LICENSE                              # MIT License
```

## Development

### Requirements

- .NET 8.0 SDK
- Lidarr nightly build
- Lidarr source code (for development)

### Building

1. Clone the Lidarr source code and place it in the Submodules folder
2. Build the plugin:

```bash
dotnet build src/MusicEnhancer/MusicEnhancer.csproj
```

### Testing

The plugin uses Lidarr's plugin framework. Test by:

1. Building the plugin
2. Copying the DLL to Lidarr's Plugins folder
3. Restarting Lidarr
4. Configuring the plugin in Settings → Connect

## Version Detection Logic

The plugin uses pattern matching to identify different version types:

### Remix Detection
- Remix, Edit, Bootleg, Reinterpretation, Rework, Remake
- Extended Mix, Club Mix, Dance Mix, House Mix, Techno Mix, Trance Mix, Dub Mix
- Instrumental, Flip

### Edit Detection
- Edit, Radio Edit, Album Edit, Single Edit, Short Edit
- Compact Edit, Clean Edit, Intro Edit, Outro Edit, Cut
- Extended, Extended Version

### Version Detection
- Version, Alternative Version, Alternate Version, Different Version
- Special Version, Unique Version, Exclusive Version

### Live Version Detection
- Live, Live Version, Live At, Live From, Live In, Live On
- Acoustic Live, Electric Live, Unplugged

### Acoustic Detection
- Acoustic, Acoustic Version, Unplugged

### Demo Detection
- Demo, Demo Version, Demo Recording, Demo Session, Demo Take

## Track-Level Unmonitoring

A key feature of Music Enhancer is the ability to unmonitor individual tracks within an album:

- When a mixed album contains both original tracks and versions, only the version tracks are unmonitored
- Original tracks remain monitored and downloaded
- This is perfect for albums like "Greatest Hits" or "Deluxe Editions"

## License

MIT License - See LICENSE file for details
