# Quick Start Guide

Get up and running with the Music Enhancer plugin in minutes.

## Prerequisites

- Lidarr nightly build (plugin support not in stable release)
- .NET 8.0 SDK (for building)

## Installation

### Option 1: Build from Source

1. Clone this repository:
   ```bash
   git clone https://github.com/waynebowie99/lidarr-plugin-musicenhancer.git
   cd lidarr-plugin-musicenhancer
   ```

2. Clone Lidarr source code:
   ```bash
   git clone https://github.com/Lidarr/Lidarr.git Submodules/Lidarr
   ```

3. Build the plugin:
   ```bash
   # Windows
   build.bat
   
   # Linux/macOS
   chmod +x build.sh
   ./build.sh
   ```

4. Copy the DLL to Lidarr's Plugins folder:
   ```bash
   # Windows
   copy src\MusicEnhancer\bin\Debug\net8.0\Lidarr.Plugin.MusicEnhancer.dll "C:\Path\To\Lidarr\Plugins\"
   
   # Linux/macOS
   cp src/MusicEnhancer/bin/Debug/net8.0/Lidarr.Plugin.MusicEnhancer.dll /path/to/lidarr/Plugins/
   ```

5. Restart Lidarr

### Option 2: Download Pre-built

1. Download the latest release DLL
2. Copy to Lidarr's Plugins folder
3. Restart Lidarr

## Configuration

1. Open Lidarr
2. Go to **Settings → Connect**
3. Click the **+** button to add a new connection
4. Select **Content Filter** from the list
5. Configure your settings (see below)
6. Click **Save**

### Recommended Settings

**For most users:**
- Version filter action: `Flag for review`
- Release types to filter: `Album, EP`
- Duration tolerance: `3000 ms`
- Enable all version type filters: `On`
- Minimum track duration: `30000 ms` (30 seconds)
- Maximum track duration: `600000 ms` (10 minutes)
- **Delete files after unmonitor: `On`** (default - files will be deleted)

**For aggressive filtering:**
- Version filter action: `Unmonitor`
- Enable all version type filters: `On`
- **Delete files after unmonitor: `On`**
- Review flagged items in Lidarr's log

**For conservative filtering:**
- Version filter action: `Flag for review`
- Only enable specific version types you care about
- **Delete files after unmonitor: `On`**
- Review each flagged item manually

**To keep files after unmonitoring:**
- Version filter action: `Unmonitor`
- **Delete files after unmonitor: `Off`**
- Files will remain on disk after unmonitoring

## Usage

### Automatic Filtering

The plugin runs automatically when:

1. An album/EP is imported
2. A release is added to Lidarr
3. A full library scan is performed

### Manual Review

1. Check Lidarr's log for flagged items
2. Search for "ContentFilter" in the log
3. Review flagged albums in the web UI
4. Manually unmonitor or delete unwanted versions

### Viewing Logs

Search the Lidarr log for:
- `ContentFilter version-analysis` - Version analysis results
- `ContentFilter track-version` - Individual track matches
- `ContentFilter unmonitor success` - Unmonitored albums
- `ContentFilter skip` - Skipped albums

## Common Use Cases

### Use Case 1: Remove Remix Singles

**Problem**: You have remix singles that you don't want in your library

**Solution**:
1. Enable remix filtering
2. Set version filter action to `Unmonitor`
3. Let the plugin automatically unmonitor remix singles

### Use Case 2: Keep Originals, Filter Versions

**Problem**: You have albums with both original tracks and versions

**Solution**:
1. Set version filter action to `Flag for review`
2. Review flagged albums manually
3. Unmonitor albums where most tracks are versions

### Use Case 3: Clean Up Live Albums

**Problem**: You have live albums mixed with studio albums

**Solution**:
1. Enable live version filtering
2. Set release types to filter: `Album`
3. Let the plugin identify live albums

### Use Case 4: Remove Demo Tracks

**Problem**: Demo tracks are mixed with final releases

**Solution**:
1. Enable demo filtering
2. Set version filter action to `Unmonitor`
3. Let the plugin automatically unmonitor demo tracks

## Troubleshooting

### Plugin Not Showing Up

- Ensure you're using Lidarr nightly build
- Check Lidarr's log for plugin loading errors
- Verify DLL is in the correct Plugins folder

### No Versions Detected

- Check that version type filters are enabled
- Verify track titles contain version keywords
- Adjust duration tolerance if needed

### False Positives

- Increase duration tolerance
- Disable specific version type filters
- Set version filter action to `Flag for review`

### False Negatives

- Decrease duration tolerance
- Enable all version type filters
- Check track title patterns

## Tips

1. **Start Conservative**: Begin with `Flag for review` to understand what the plugin detects
2. **Review Logs**: Check Lidarr's log to understand detection decisions
3. **Test on Small Library**: Test on a small portion of your library first
4. **Adjust Over Time**: Fine-tune settings based on your library's patterns
5. **Backup First**: Always backup your Lidarr database before major changes

## Support

- Check the Lidarr community forums
- Review the log for detailed detection information
- Report issues with reproduction steps

## Next Steps

1. Configure the plugin to match your preferences
2. Run a library scan to see what gets detected
3. Review flagged items manually
4. Adjust settings based on your findings
5. Enjoy a cleaner, more organized music library!
