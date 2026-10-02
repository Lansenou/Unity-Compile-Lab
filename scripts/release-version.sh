#!/usr/bin/env bash
# Prints the release version for a CI run (docs/status.md, "Releases"):
#   v0.<minor>.<run number>  on a push to main
#   dev-<sha>                otherwise
# <minor> comes from <Version> in Directory.Build.props, the one checked-in place.
#   scripts/release-version.sh <event name> <ref> <run number> <sha>
set -euo pipefail
cd "$(dirname "$0")/.."
event=${1:?usage: release-version.sh <event> <ref> <run number> <sha>}
ref=${2:?}
run=${3:?}
sha=${4:?}
minor=$(sed -n 's|.*<Version>0\.\([0-9][0-9]*\)\.[0-9][0-9]*</Version>.*|\1|p' Directory.Build.props)
case $minor in
  '' | *[!0-9]*) echo "no 0.<minor>.<patch> Version in Directory.Build.props" >&2; exit 1 ;;
esac
if [ "$event" = push ] && [ "$ref" = refs/heads/main ]; then
  echo "v0.$minor.$run"
else
  echo "dev-$sha"
fi
