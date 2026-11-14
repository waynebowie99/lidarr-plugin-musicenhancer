#!/bin/bash

# Lidarr Music Enhancer Plugin Build Script

set -e

echo "=== Lidarr Music Enhancer Plugin Build ==="
echo ""

# Download the verified Lidarr reference assemblies used for compilation
echo "Preparing Lidarr reference assemblies..."
python3 scripts/prepare-lidarr.py

# Build the plugin
echo "Building Music Enhancer plugin..."
dotnet build src/MusicEnhancer/MusicEnhancer.csproj

echo ""
echo "=== Build Successful ==="
echo ""
echo "Plugin DLL location:"
echo "  src/MusicEnhancer/bin/Debug/net8.0/Lidarr.Plugin.MusicEnhancer.dll"
echo ""
echo "To install in Lidarr:"
echo "  1. Copy the DLL to Lidarr's Plugins folder"
echo "  2. Restart Lidarr"
echo "  3. Configure it in Lidarr's settings"
