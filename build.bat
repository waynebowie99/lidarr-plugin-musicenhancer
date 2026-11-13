@echo off
REM Lidarr Content Filter Plugin Build Script

echo === Lidarr Content Filter Plugin Build ===
echo.

REM Check if Lidarr submodule exists
if not exist "Submodules\Lidarr" (
    echo Error: Lidarr submodule not found!
    echo Please clone Lidarr source code first:
    echo   git clone https://github.com/Lidarr/Lidarr.git Submodules\Lidarr
    exit /b 1
)

REM Build the plugin
echo Building Content Filter plugin...
dotnet build src\ContentFilter\ContentFilter.csproj

if %ERRORLEVEL% EQU 0 (
    echo.
    echo === Build Successful ===
    echo.
    echo Plugin DLL location:
    echo   src\ContentFilter\bin\Debug\net8.0\Lidarr.Plugin.ContentFilter.dll
    echo.
    echo To install in Lidarr:
    echo   1. Copy the DLL to Lidarr's Plugins folder
    echo   2. Restart Lidarr
    echo   3. Configure in Settings -> Connect
) else (
    echo.
    echo === Build Failed ===
    exit /b 1
)
