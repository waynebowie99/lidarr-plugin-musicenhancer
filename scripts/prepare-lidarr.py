#!/usr/bin/env python3
"""Prepare verified, prebuilt Lidarr references without installing or running Lidarr."""

import hashlib
import platform
import shutil
import sys
import tarfile
import tempfile
import urllib.request
import zipfile
from pathlib import Path, PurePosixPath


VERSION = "3.2.1.5196"
# GitHub release asset digests, not checksums fetched alongside the download.
# https://github.com/Lidarr/Lidarr/releases/tag/v3.2.1.5196
SHA256 = {
    "linux-core-x64": "009ce48af3faab34578dd1fcd3945cae7db42f5449c7999e2ffd20d60a22ec06",
    "linux-core-arm64": "f42596fe026730fdd1c67bdded3bc5394e8f89fdef70db5fc52b71267d6a2dd8",
    "osx-core-x64": "9e9a5fae390301897a5d87bbdc6f55506d6684bafc28dc7c297b4c46d3dc9bb2",
    "osx-core-arm64": "1291678c58e0210f2a4290d95a3130318821d12f0136754258a5d82b23120ce4",
    "windows-core-x64": "be4cd5452786c726c7e4f4214d565109a3d1c76ad93a2d50bb2a340bb9a50a9f",
}
REQUIRED = {
    "Lidarr.Core.dll", "Lidarr.Common.dll", "Lidarr.Http.dll", "Lidarr.Api.V1.dll",
    "NLog.dll", "FluentValidation.dll", "Newtonsoft.Json.dll", "System.IO.Abstractions.dll",
    "TagLibSharp.dll",
}


def verify_archive(archive, expected):
    with archive.open("rb") as stream:
        actual = hashlib.file_digest(stream, "sha256").hexdigest()
    if actual != expected:
        raise ValueError(f"SHA-256 mismatch for {archive.name}: expected {expected}, got {actual}")


def extract_assemblies(archive, destination):
    """Copy only regular DLL files immediately inside Lidarr/, never archive paths."""
    found = set()
    if archive.name.endswith(".zip"):
        with zipfile.ZipFile(archive) as bundle:
            for member in bundle.infolist():
                path = PurePosixPath(member.filename)
                if len(path.parts) != 2 or path.parts[0] != "Lidarr" or path.suffix != ".dll":
                    continue
                if member.is_dir() or path.name in found:
                    raise ValueError(f"Invalid or duplicate assembly member: {member.filename}")
                if member.file_size > 100 * 1024 * 1024:
                    raise ValueError(f"Oversized assembly: {member.filename}")
                with bundle.open(member) as source, (destination / path.name).open("wb") as target:
                    shutil.copyfileobj(source, target)
                found.add(path.name)
    else:
        with tarfile.open(archive, "r:gz") as bundle:
            for member in bundle:
                path = PurePosixPath(member.name)
                if len(path.parts) != 2 or path.parts[0] != "Lidarr" or path.suffix != ".dll":
                    continue
                if not member.isfile() or path.name in found:
                    raise ValueError(f"Invalid or duplicate assembly member: {member.name}")
                if member.size > 100 * 1024 * 1024:
                    raise ValueError(f"Oversized assembly: {member.name}")
                with bundle.extractfile(member) as source, (destination / path.name).open("wb") as target:
                    shutil.copyfileobj(source, target)
                found.add(path.name)
    missing = REQUIRED - found
    if missing:
        raise ValueError(f"Required Lidarr assemblies missing: {', '.join(sorted(missing))}")
    return len(found)


def main():
    system = {"Darwin": "osx", "Linux": "linux", "Windows": "windows"}.get(platform.system())
    machine = {"arm64": "arm64", "aarch64": "arm64", "x86_64": "x64", "AMD64": "x64"}.get(platform.machine())
    rid = f"{system}-core-{machine}"
    if rid not in SHA256:
        raise ValueError("Supported build hosts: Linux, macOS, or Windows, x64 or arm64")

    root = Path(__file__).resolve().parent.parent
    cache = root / "_temp" / "lidarr"
    cache.mkdir(parents=True, exist_ok=True)
    suffix = ".zip" if rid.startswith("windows-") else ".tar.gz"
    name = f"Lidarr.develop.{VERSION}.{rid}{suffix}"
    archive = cache / name
    if not archive.exists():
        url = f"https://github.com/Lidarr/Lidarr/releases/download/v{VERSION}/{name}"
        print(f"Downloading {url}", flush=True)
        with tempfile.TemporaryDirectory(dir=cache) as temporary:
            download = Path(temporary) / name
            request = urllib.request.Request(url, headers={"User-Agent": "MusicEnhancer-build"})
            with urllib.request.urlopen(request, timeout=60) as response, download.open("wb") as target:
                total = 0
                while chunk := response.read(1024 * 1024):
                    total += len(chunk)
                    if total > 256 * 1024 * 1024:
                        raise ValueError("Lidarr archive exceeds 256 MiB")
                    target.write(chunk)
            verify_archive(download, SHA256[rid])
            download.replace(archive)

    # Reverify cached archives and re-extract so edited DLLs cannot become references.
    verify_archive(archive, SHA256[rid])
    destination = cache / VERSION
    if destination.is_symlink():
        raise ValueError("Refusing to replace a symlink at the generated reference directory")
    with tempfile.TemporaryDirectory(dir=cache) as temporary:
        staged = Path(temporary) / VERSION
        staged.mkdir()
        count = extract_assemblies(archive, staged)
        if destination.exists():
            shutil.rmtree(destination)
        staged.replace(destination)
    print(f"Prepared {count} verified Lidarr {VERSION} assemblies in {destination}")


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, tarfile.TarError, zipfile.BadZipFile) as error:
        sys.exit(f"Lidarr preparation failed: {error}")
