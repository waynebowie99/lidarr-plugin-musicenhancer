# Development

Requirements: .NET 8 SDK and Python 3. The plugin builds against verified prebuilt Lidarr assemblies, without a Lidarr submodule.

```bash
python3 scripts/prepare-lidarr.py
dotnet build src/MusicEnhancer/MusicEnhancer.csproj -c Release
dotnet run --project tests/MusicEnhancer.Tests/MusicEnhancer.Tests.csproj -c Release
```

Windows can use `python` instead of `python3`. Release builds produce the DLL and `Lidarr.Plugin.MusicEnhancer.net8.0.zip` in `src/MusicEnhancer/bin/Release/net8.0/`. The release workflow runs the offline tests before publishing.

Tests cover metadata round trips, preservation, online lookup responses, startup behavior, title matching and destructive-action safety. `MUSICENHANCER_LIVE_TEST=1` enables optional network-dependent checks; the regular suite does not require external services.

`MusicEnhancerConnection` handles imports; `MusicEnhancerStartupService` handles opt-in startup scans. `ContentFilterService` applies version actions and `MetadataEnhancerService` handles opt-in tag writing. See the [README](README.md) for supported fields, limitations and safety behavior.
