#!/usr/bin/env bash
# Builds release artifacts into OUT:
#   ucl-<rid>.tar.gz / ucl-<rid>.zip  self-contained single-file binaries
#   UnityCompileLab.Tool.<version>.nupkg  the .NET global tool
#   LICENSE, NOTICE, SHA256SUMS
#
#   scripts/build-release.sh <version> <out> [rid...]
# <version> is a tag such as v0.1.0 (or dev-<sha> for CI builds). Default rids:
# win-x64 osx-arm64 osx-x64 linux-x64. With explicit rids only those binaries are
# built, unpacked in OUT/ucl-<rid>/ for smoke tests, and nothing is archived.
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

tag=${1:?usage: build-release.sh <version> <out> [rid...]}
out=${2:?usage: build-release.sh <version> <out> [rid...]}
shift 2
case $tag in
  v[0-9]*) version=${tag#v} ;;
  *) version="0.0.0-${tag//[^0-9A-Za-z.-]/-}" ;;
esac

full=1
rids=(win-x64 osx-arm64 osx-x64 linux-x64)
if [ $# -gt 0 ]; then
  full=0
  rids=("$@")
fi

rm -rf "$out"
mkdir -p "$out"
for rid in "${rids[@]}"; do
  echo "== publish $rid ($version)"
  dotnet publish src/Ucl.Cli -c Release -r "$rid" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=none -p:Version="$version" -p:PackAsTool=false \
    -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false \
    -o "$out/ucl-$rid" >/dev/null
  cp LICENSE NOTICE THIRD_PARTY_NOTICES.md "$out/ucl-$rid/"
done

if [ "$full" = 1 ]; then
  echo "== pack global tool"
  dotnet pack src/Ucl.Cli -c Release -p:Version="$version" -o "$out" >/dev/null
  cp LICENSE NOTICE "$out/"
  (
    cd "$out"
    for rid in "${rids[@]}"; do
      if [ "${rid%%-*}" = win ]; then
        if command -v zip >/dev/null; then zip -qr "ucl-$rid.zip" "ucl-$rid"; else powershell -NoProfile -Command "Compress-Archive -Path ucl-$rid -DestinationPath ucl-$rid.zip"; fi
      else
        tar -czf "ucl-$rid.tar.gz" "ucl-$rid"
      fi
      rm -rf "ucl-$rid"
    done
    sha256sum -- * | grep -v ' SHA256SUMS$' > SHA256SUMS
  )
fi
ls -l "$out"
