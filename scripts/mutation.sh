#!/usr/bin/env bash
# Mutation check: proves the fixture corpus tests the define evaluator. Copies the repository (without
# bin/, obj/, artifacts/, coverage/, .git/) to a temporary folder, deletes the line that adds UNITY_ANDROID
# from src/Ucl.Core/Rules/DefineTable.cs in the copy, builds it and runs the fixture integration tests.
# Succeeds only if at least one test fails ("mutation killed"); exit 1 if they all pass ("mutant survived").
#   scripts/mutation.sh
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
root=$PWD
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
copy="$tmp/repo"

mkdir -p "$copy"
# tar keeps this portable to Git Bash (no rsync there).
tar -C "$root" --exclude=./.git --exclude='*/bin' --exclude='*/obj' --exclude=./artifacts --exclude=./coverage -cf - . | tar -C "$copy" -xf -

table="$copy/src/Ucl.Core/Rules/DefineTable.cs"
before=$(cksum <"$table")
sed -i.orig '/"UNITY_ANDROID"/d' "$table"
rm -f "$table.orig"
if [ "$before" = "$(cksum <"$table")" ]; then
  echo "mutation: FAILED TO APPLY: no line with \"UNITY_ANDROID\" in src/Ucl.Core/Rules/DefineTable.cs" >&2
  exit 2
fi

cd "$copy"
dotnet build Ucl.slnx -c Release -nologo -v quiet >"$tmp/build.log" 2>&1 || { cat "$tmp/build.log"; echo "mutation: the mutant does not build" >&2; exit 2; }
dotnet run --project tests/Ucl.StubBuilder -c Release --no-build -- artifacts/stubs >/dev/null

set +e
dotnet test tests/Ucl.Integration.Tests -c Release --no-build --filter 'FullyQualifiedName~FixtureTests' >"$tmp/test.log" 2>&1
code=$?
set -e
summary=$(grep -E '(Failed|Passed)!|Total tests|Failed:' "$tmp/test.log" | tail -n 1 || true)
if [ "$code" -ne 0 ] && grep -qE 'Failed[!:]? *[-:]? *[1-9]|\[FAIL\]' "$tmp/test.log"; then
  echo "mutation: killed (UNITY_ANDROID removed, fixture tests fail) ${summary}"
  exit 0
fi
if [ "$code" -ne 0 ]; then
  tail -n 40 "$tmp/test.log"
  echo "mutation: test run failed without a failing test (exit $code)" >&2
  exit 2
fi
echo "mutation: SURVIVED (UNITY_ANDROID removed, every fixture test still passes) ${summary}"
exit 1
