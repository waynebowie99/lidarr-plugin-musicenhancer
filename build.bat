@echo off
REM Lidarr Music Enhancer Plugin Build Script

echo === Lidarr Music Enhancer Plugin Build ===
echo.

REM Download the verified Lidarr reference assemblies used for compilation
echo Preparing Lidarr reference assemblies...
python scripts\prepare-lidarr.py
if %ERRORLEVEL% NEQ 0 exit /b 1

REM Build the plugin
echo Building Music Enhancer plugin...
dotnet build src\MusicEnhancer\MusicEnhancer.csproj

if %ERRORLEVEL% EQU 0 (
    echo.
    echo === Build Successful ===
    echo.
    echo Plugin DLL location:
    echo   src\MusicEnhancer\bin\Debug\net8.0\Lidarr.Plugin.MusicEnhancer.dll
    echo.
    echo To install in Lidarr:
    echo   1. Copy the DLL to Lidarr's Plugins folder
    echo   2. Restart Lidarr
    echo   3. Configure it in Lidarr's settings
) else (
    echo.
    echo === Build Failed ===
    exit /b 1
)
