#!/usr/bin/env bash
# R11 benchmark: generates the project (scripts/gen-bench.sh), then times a cold
# full editor compile, two warm no-change runs, a one-file method-body edit and a
# one-file public-API edit at the bottom of the graph. Prints a markdown table.
#   scripts/bench.sh [work-dir]
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
work=${1:-$(mktemp -d)}
dotnet build src/Ucl.Cli -c Release >/dev/null
dotnet run --project tests/Ucl.StubBuilder -c Release -- artifacts/stubs >/dev/null
export UCL_EDITOR_ROOTS="$PWD/artifacts/stubs/editors"
scripts/gen-bench.sh "$work/bench" >/dev/null
ucl() { dotnet src/Ucl.Cli/bin/Release/net10.0/ucl.dll "$@"; }
now() { date +%s%N; }

failed=0
run() { # run <label> <limit-seconds>
  local s e
  s=$(now)
  ucl check "$work/bench" --editor-os linux --cache-dir "$work/cache" > "$work/out.txt" 2>&1 || { cat "$work/out.txt"; exit 1; }
  e=$(now)
  printf '| %s | %d.%02d s | %s s |\n' "$1" $(((e - s) / 1000000000)) $((((e - s) / 10000000) % 100)) "$2"
  if [ $(((e - s) / 1000000)) -gt $(($2 * 1000)) ]; then failed=1; fi
}

echo "| run | wall clock | target |"
echo "|---|---|---|"
run "cold, full editor compile (32 assemblies, 1,720 files)" 60
run "warm, no change" 3
run "warm, no change (again)" 3
printf '\n// edited\n' >> "$work/bench/Assets/Game/Runtime/R5/R5Type7.cs"
run "one file changed (method body, Runtime layer)" 10
sed -i.bak 's/public float Speed => speed;/public float Speed => speed; public int Added => 1;/' "$work/bench/Assets/Game/Core/A/AType3.cs"
run "one file changed (public API, bottom of the graph)" 10
echo
echo "$(nproc 2>/dev/null || sysctl -n hw.ncpu) cores, $(uname -s), $(dotnet --version)"
if [ "$failed" = 1 ]; then echo "a run exceeded its R11 target"; exit 1; fi
