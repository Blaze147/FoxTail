#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
runtime="${1:-win-x64}"
case "$runtime" in win-x64|win-arm64) ;; *) echo 'Supported Windows targets: win-x64, win-arm64'; exit 1 ;; esac
# Stage into a fresh folder so previous framework dependencies cannot enter the ZIP.
stage="$(mktemp -d "${TMPDIR:-/tmp}/foxtail-publish.XXXXXX")"
trap 'rm -rf "$stage"' EXIT
dotnet publish src/FoxTail.Desktop -c Release -r "$runtime" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o "$stage"
cp README.md "$stage/"
mkdir -p "artifacts/$runtime"
cp -R "$stage/." "artifacts/$runtime/"
archive="$PWD/artifacts/FoxTail-$runtime.zip"
rm -f "$archive"
(cd "$stage" && zip -q -r "$archive" .)
echo "Portable Windows package: $archive"
