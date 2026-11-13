#!/bin/bash

# Lidarr Content Filter Plugin Build Script

set -e

echo "=== Lidarr Content Filter Plugin Build ==="
echo ""

# Check if Lidarr submodule exists
if [ ! -d "Submodules/Lidarr" ]; then
    echo "Error: Lidarr submodule not found!"
    echo "Please clone Lidarr source code first:"
    echo "  git clone https://github.com/Lidarr/Lidarr.git Submodules/Lidarr"
    exit 1
fi

# Build the plugin
echo "Building Content Filter plugin..."
dotnet build src/ContentFilter/ContentFilter.csproj

if [ $? -eq 0 ]; then
    echo ""
    echo "=== Build Successful ==="
    echo ""
    echo "Plugin DLL location:"
    echo "  src/ContentFilter/bin/Debug/net8.0/Lidarr.Plugin.ContentFilter.dll"
    echo ""
    echo "To install in Lidarr:"
    echo "  1. Copy the DLL to Lidarr's Plugins folder"
    echo "  2. Restart Lidarr"
    echo "  3. Configure in Settings → Connect"
else
    echo ""
    echo "=== Build Failed ==="
    exit 1
fi
