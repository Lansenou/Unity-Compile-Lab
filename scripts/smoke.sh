#!/usr/bin/env bash
# Smoke test of a built ucl binary against committed fixtures and the stub editors.
#   scripts/smoke.sh <path-to-ucl> [<expected version>]
set -euo pipefail
cd "$(dirname "$0")/.."
bin=${1:?usage: smoke.sh <path-to-ucl>}
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

dotnet run --project tests/Ucl.StubBuilder -c Release -- artifacts/stubs >/dev/null
export UCL_EDITOR_ROOTS="$PWD/artifacts/stubs/editors"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

expect() { # expect <code> <grep-pattern> <args...>
  local want=$1 pattern=$2; shift 2
  set +e
  "$bin" "$@" --cache-dir "$tmp/cache" >"$tmp/out" 2>&1
  local got=$?
  set -e
  if [ "$got" != "$want" ] || ! grep -q -- "$pattern" "$tmp/out"; then
    echo "FAIL: ucl $* -> exit $got (want $want, pattern '$pattern')"; cat "$tmp/out"; exit 1
  fi
  echo "ok: ucl $* -> exit $got"
}

"$bin" version
if [ -n "${2:-}" ] && [ "$("$bin" version | tr -d '\r')" != "ucl $2" ]; then
  echo "FAIL: ucl version is '$("$bin" version)', want 'ucl $2'"; exit 1
fi
"$bin" --help | grep -q "exit codes"
expect 0 "exit 0" check fixtures/basic-predefined
expect 1 "error CS0103" check fixtures/compile-error
expect 0 '"schema": "ucl-result/1"' check fixtures/basic-predefined --format json
expect 0 '"version": "2.1.0"' check fixtures/basic-predefined --format sarif
expect 0 "Assembly-CSharp" graph fixtures/basic-predefined
expect 3 "UCL3001" check "$tmp"

# ucl test: NUnit inside the binary, test assemblies loaded from a temporary folder.
cp -r fixtures/test-editmode "$tmp/tests"
mkdir -p "$tmp/tests/Packages/com.unity.ext.nunit/net40/unity-custom"
cp artifacts/stubs/dlls/nunit.framework.dll "$tmp/tests/Packages/com.unity.ext.nunit/net40/unity-custom/"
expect 1 "result: 45 cases: 25 passed, 1 failed, 2 skipped, 1 ignored, 11 needs-unity, 5 unity-only" test "$tmp/tests" --editor-os linux

# The test host is this binary started again: a test that kills it loses no case.
cp -r fixtures/test-host-crash "$tmp/crash"
cp artifacts/stubs/dlls/nunit.framework.dll "$tmp/crash/Packages/com.unity.ext.nunit/net40/unity-custom/"
expect 1 "test host crashed after Game.Tests.RenderTests.C_pooled_command_buffer" test "$tmp/crash" --editor-os linux
echo "smoke test passed"
