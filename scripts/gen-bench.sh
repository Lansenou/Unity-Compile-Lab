#!/usr/bin/env bash
# Generates the R11 benchmark project (not committed): 30 asmdefs in Core, Runtime,
# Editor and Tests layers plus two embedded packages, ~1,700 C# files of realistic
# shape, one RoslynAnalyzer DLL scoped to the Core layer and one noisy analyzer that
# runs on every assembly.
#
#   scripts/gen-bench.sh <out-dir>
# Uses only the stub API of fixtures/_stubs; the analyzer DLL comes from
# artifacts/stubs/dlls (built by tests/Ucl.StubBuilder). Deterministic output.
set -euo pipefail
cd "$(dirname "$0")/.."
out=${1:?usage: gen-bench.sh <out-dir>}
rm -rf "$out"
mkdir -p "$out/Assets" "$out/Packages" "$out/ProjectSettings"

printf 'm_EditorVersion: 6000.0.30f1\nm_EditorVersionWithRevision: 6000.0.30f1 (0123456789ab)\n' > "$out/ProjectSettings/ProjectVersion.txt"
cat > "$out/ProjectSettings/ProjectSettings.asset" <<'YAML'
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!129 &1
PlayerSettings:
  productName: Bench
  scriptingDefineSymbols:
    Standalone: BENCH_FEATURE_A;BENCH_FEATURE_B
  apiCompatibilityLevel: 6
  activeInputHandler: 0
  suppressCommonWarnings: 1
YAML
cat > "$out/Packages/manifest.json" <<'JSON'
{
  "dependencies": {
    "com.bench.utils": "file:com.bench.utils",
    "com.bench.net": "file:com.bench.net",
    "com.unity.modules.physics": "1.0.0"
  }
}
JSON

guid() { printf '%032x' "$1"; }
n=0
asmdef() { # asmdef <dir> <name> <refs-json> [extra-json]
  n=$((n + 1))
  mkdir -p "$1"
  printf '{\n  "name": "%s",\n  "references": [%s]%s\n}\n' "$2" "$3" "${4:-}" > "$1/$2.asmdef"
  printf 'fileFormatVersion: 2\nguid: %s\nAssemblyDefinitionImporter:\n  externalObjects: {}\n' "$(guid $((1000 + n)))" > "$1/$2.asmdef.meta"
}

# file <dir> <namespace> <class> <index> <dependency-namespace-or-empty> <editor 0|1>
file() {
  local dir=$1 ns=$2 cls=$3 i=$4 dep=$5 editor=$6
  {
    echo "using System;"
    echo "using System.Collections.Generic;"
    echo "using UnityEngine;"
    [ "$editor" = 1 ] && echo "using UnityEditor;"
    [ -n "$dep" ] && echo "using $dep;"
    echo
    echo "namespace $ns"
    echo "{"
    echo "    /// <summary>Generated benchmark type $i.</summary>"
    if [ "$editor" = 1 ]; then
      echo "    public class $cls : EditorWindow"
      echo "    {"
      echo "        [MenuItem(\"Bench/$ns/$cls\")]"
      echo "        private static void Open() => GetWindow<$cls>();"
    else
      echo "    public class $cls : MonoBehaviour"
      echo "    {"
    fi
    echo "        [SerializeField] private float speed = $((i % 17 + 1))f;"
    echo "        [SerializeField] private int count = $((i % 11));"
    echo "        private readonly List<Vector3> points = new List<Vector3>();"
    echo "        private readonly Dictionary<string, int> lookup = new Dictionary<string, int>();"
    echo
    echo "        public float Speed => speed;"
    echo
    echo "        public Vector3 Step(Vector3 from, Vector3 to, float t)"
    echo "        {"
    echo "            var clamped = Math.Max(0f, Math.Min(1f, t));"
    echo "            var next = Vector3.Lerp(from, to, clamped);"
    echo "            points.Add(next);"
    echo "            if (points.Count > $((i % 50 + 10)))"
    echo "            {"
    echo "                points.RemoveAt(0);"
    echo "            }"
    echo
    echo "            return next;"
    echo "        }"
    echo
    echo "        public int Accumulate(IEnumerable<int> values)"
    echo "        {"
    echo "            var total = 0;"
    echo "            foreach (var v in values)"
    echo "            {"
    echo "                total += v % (count + 1);"
    echo "                lookup[\"k\" + (v % 7)] = total;"
    echo "            }"
    echo
    echo "#if BENCH_FEATURE_A"
    echo "            total += lookup.Count;"
    echo "#endif"
    echo "            return total;"
    echo "        }"
    echo
    echo "        public float Distance(Vector3 a, Vector3 b) => (a - b).magnitude * speed;"
    [ -n "$dep" ] && echo "        public string Describe() => nameof($cls) + \":\" + typeof(Marker).Name;"
    echo "    }"
    echo "}"
  } > "$dir/$cls.cs"
}

marker() { # marker <dir> <namespace>
  printf 'namespace %s\n{\n    /// <summary>Layer marker.</summary>\n    public sealed class Marker\n    {\n    }\n}\n' "$2" > "$1/Marker.cs"
}

layer() { # layer <dir> <asmdef-name> <namespace> <refs-json> <files> <dep-ns> <editor> [extra]
  asmdef "$1" "$2" "$4" "${8:-}"
  marker "$1" "$3"
  local k
  for k in $(seq 1 "$5"); do
    file "$1" "$3" "${3##*.}Type$k" "$k" "$6" "$7"
  done
}

# Packages (2 asmdefs, 30 files each).
for p in utils net; do
  root="$out/Packages/com.bench.$p"
  mkdir -p "$root/Runtime"
  printf '{\n  "name": "com.bench.%s",\n  "version": "1.%s.0",\n  "displayName": "Bench %s"\n}\n' "$p" "${#p}" "$p" > "$root/package.json"
done
layer "$out/Packages/com.bench.utils/Runtime" Bench.Utils Bench.Utils "" 29 "" 0
layer "$out/Packages/com.bench.net/Runtime" Bench.Net Bench.Net '"Bench.Utils"' 29 Bench.Utils 0 \
  ',
  "versionDefines": [{ "name": "com.bench.utils", "expression": "1.0", "define": "BENCH_UTILS" }]'

# Core: 6 asmdefs in a chain, 60 files each.
prev=""
for c in A B C D E F; do
  refs='"Bench.Utils"'
  [ -n "$prev" ] && refs="$refs, \"Game.Core.$prev\""
  layer "$out/Assets/Game/Core/$c" "Game.Core.$c" "Game.Core.$c" "$refs" 59 Bench.Utils 0
  prev=$c
done

# The analyzer is scoped to Game.Core.A and every assembly that references it.
mkdir -p "$out/Assets/Game/Core/A/Analyzers"
cp artifacts/stubs/dlls/Ucl.Fixture.Analyzer.dll "$out/Assets/Game/Core/A/Analyzers/"
printf 'fileFormatVersion: 2\nguid: %s\nlabels:\n- RoslynAnalyzer\nPluginImporter:\n  serializedVersion: 2\n  isExplicitlyReferenced: 0\n  platformData:\n  - first:\n      Any: \n    second:\n      enabled: 0\n      settings: {}\n' "$(guid 99)" \
  > "$out/Assets/Game/Core/A/Analyzers/Ucl.Fixture.Analyzer.dll.meta"

# A noisy analyzer outside every asmdef folder runs on every assembly, as large third-party analyzer sets do.
mkdir -p "$out/Assets/Plugins/Analyzers"
cp artifacts/stubs/dlls/Ucl.Bench.Noisy.dll "$out/Assets/Plugins/Analyzers/"
printf 'fileFormatVersion: 2\nguid: %s\nlabels:\n- RoslynAnalyzer\nPluginImporter:\n  serializedVersion: 2\n  isExplicitlyReferenced: 0\n  platformData:\n  - first:\n      Any: \n    second:\n      enabled: 0\n      settings: {}\n' "$(guid 98)" \
  > "$out/Assets/Plugins/Analyzers/Ucl.Bench.Noisy.dll.meta"

# Runtime: 12 asmdefs, 70 files each, each referencing two Core assemblies and the net package.
cores=(A B C D E F)
for r in $(seq 1 12); do
  c1=${cores[$((r % 6))]} c2=${cores[$(((r + 3) % 6))]}
  layer "$out/Assets/Game/Runtime/R$r" "Game.Runtime.R$r" "Game.Runtime.R$r" "\"Game.Core.$c1\", \"Game.Core.$c2\", \"Bench.Net\"" 69 "Game.Core.$c1" 0
done

# Editor: 6 Editor-only asmdefs, 40 files each.
for e in $(seq 1 6); do
  layer "$out/Assets/Game/Editor/E$e" "Game.Editor.E$e" "Game.Editor.E$e" "\"Game.Runtime.R$e\", \"Game.Runtime.R$((e + 6))\"" 39 "Game.Runtime.R$e" 1 \
    ',
  "includePlatforms": ["Editor"]'
done

# Tests: 4 Editor-only, not auto-referenced, 25 files each.
for t in $(seq 1 4); do
  layer "$out/Assets/Tests/T$t" "Game.Tests.T$t" "Game.Tests.T$t" "\"Game.Runtime.R$t\"" 24 "Game.Runtime.R$t" 0 \
    ',
  "includePlatforms": ["Editor"],
  "autoReferenced": false'
done

# Predefined assemblies: 100 runtime scripts and 20 Editor-folder scripts.
mkdir -p "$out/Assets/Scripts" "$out/Assets/Scripts/Editor"
marker "$out/Assets/Scripts" Game.Scripts
for k in $(seq 1 99); do file "$out/Assets/Scripts" Game.Scripts "ScriptType$k" "$k" Game.Core.A 0; done
for k in $(seq 1 20); do file "$out/Assets/Scripts/Editor" Game.Scripts.Editors "EditorType$k" "$k" Game.Scripts 1; done

echo "generated $(find "$out" -name '*.cs' | wc -l | tr -d ' ') C# files in $(find "$out" -name '*.asmdef' | wc -l | tr -d ' ') asmdefs under $out"
