#!/usr/bin/env bash
# Baut das Spieler-ZIP:  package/BepInEx/plugins/LinkRadio/LinkRadio.dll + README.md
# Voraussetzung: vorher ./build.sh (oder dotnet build) ausgefuehrt.
set -euo pipefail
cd "$(dirname "$0")"
DLL="${1:-bin/LinkRadio.dll}"
[ -f "$DLL" ] || DLL="bin/Release/net472/LinkRadio.dll"
VER=$(grep -oP 'Version = "\K[^"]+' src/LinkRadio.cs | head -1)
cp "$DLL" package/BepInEx/plugins/LinkRadio/LinkRadio.dll
cp README.md package/README.md
rm -f "LinkRadio_${VER}.zip"
(cd package && zip -qr "../LinkRadio_${VER}.zip" . -x "*.gitkeep")
echo "OK -> LinkRadio_${VER}.zip"
