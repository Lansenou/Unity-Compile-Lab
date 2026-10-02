#!/usr/bin/env bash
# Phase B benchmark: `ucl test` on fixture test-editmode against `dotnet test` on an equivalent hand-written
# csproj (the same sources, defines and stub engine DLLs, NUnit 3.14.0 with NUnit3TestAdapter). Prints a
# markdown table. Needs network once for the csproj's NuGet restore.
#   scripts/bench-test.sh [work-dir]
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
repo=$PWD
work=${1:-$(mktemp -d)}
mkdir -p "$work"
dotnet build src/Ucl.Cli -c Release >/dev/null
dotnet run --project tests/Ucl.StubBuilder -c Release -- artifacts/stubs >/dev/null
export UCL_EDITOR_ROOTS="$repo/artifacts/stubs/editors"

# The Unity project.
rm -rf "$work/project" "$work/csproj" "$work/cache"
cp -r fixtures/test-editmode "$work/project"
mkdir -p "$work/project/Packages/com.unity.ext.nunit/net40/unity-custom"
cp artifacts/stubs/dlls/nunit.framework.dll "$work/project/Packages/com.unity.ext.nunit/net40/unity-custom/"

# The equivalent csproj: every source of the fixture's assemblies in one test project.
managed="$repo/artifacts/stubs/editors/6000.0.30f1/Editor/Data/Managed/UnityEngine"
mkdir -p "$work/csproj"
cat > "$work/csproj/Bench.Tests.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    <DefineConstants>UNITY_EDITOR;UNITY_5_3_OR_NEWER;NET_UNITY_4_8;UNITY_INCLUDE_TESTS</DefineConstants>
    <NoWarn>CS0169;CS0649;CS0282</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$work/project/Assets/**/*.cs" />
    <Compile Include="$work/project/Packages/com.unity.test-framework/**/*.cs" />
    <Reference Include="UnityEngine.CoreModule" HintPath="$managed/UnityEngine.CoreModule.dll" />
    <Reference Include="UnityEditor.CoreModule" HintPath="$managed/UnityEditor.CoreModule.dll" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.10.1" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
  </ItemGroup>
</Project>
XML
printf '<Project />\n' > "$work/csproj/Directory.Build.props"
printf '<Project />\n' > "$work/csproj/Directory.Packages.props"
dotnet restore "$work/csproj" >/dev/null

now() { date +%s%N; }
row() { # row <label> <command...>
  local label=$1 s e
  shift
  s=$(now)
  "$@" > "$work/out.txt" 2>&1 || true
  e=$(now)
  printf '| %s | %d.%02d s | %s |\n' "$label" $(((e - s) / 1000000000)) $((((e - s) / 10000000) % 100)) "$(grep -Eo 'result: [0-9]+ cases|Total tests: [0-9]+|total: [0-9]+|Passed: +[0-9]+' "$work/out.txt" | tail -1)"
}
ucl() { dotnet src/Ucl.Cli/bin/Release/net10.0/ucl.dll test "$work/project" --editor-os linux "$@"; }

echo "| run | wall clock | cases |"
echo "|---|---|---|"
row "ucl test, no cache (compile 6 assemblies + run)" ucl --no-cache
row "ucl test, cold cache" ucl --cache-dir "$work/cache"
row "ucl test, warm cache" ucl --cache-dir "$work/cache"
row "dotnet test (build + run, after restore)" dotnet test "$work/csproj" --no-restore
row "dotnet test --no-build" dotnet test "$work/csproj" --no-build
echo
echo "$(nproc 2>/dev/null || sysctl -n hw.ncpu) cores, $(uname -s), $(dotnet --version)"
