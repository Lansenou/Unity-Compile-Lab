param(
    [Parameter(Mandatory=$true)][string]$Project,
    [Parameter(Mandatory=$true)][string]$Scratch,
    [Parameter(Mandatory=$true)][string]$Evidence
)
$ErrorActionPreference = 'Stop'
$Project = (Resolve-Path -LiteralPath $Project).Path
if (Test-Path -LiteralPath $Scratch) { throw 'Scratch must be a new directory for a cold copy.' }
New-Item -ItemType Directory -Force $Scratch,$Evidence | Out-Null
$timer = [Diagnostics.Stopwatch]::StartNew()
& "$PSScriptRoot/key.ps1" -Project $Project -Evidence $Evidence
$cacheKey = (Get-Content -LiteralPath "$Evidence/cache-key.txt" -Raw).Trim()
foreach ($folder in 'ProjectSettings','Packages','Assets') {
    robocopy "$Project/$folder" "$Scratch/$folder" /E /MT:16 /NFL /NDL /NJH /NJS /NP /R:1 /W:1
    if ($LASTEXITCODE -gt 7) { throw "Project copy failed: $LASTEXITCODE" }
}
# Preserve the common parent analyzer config referenced by project-local csc.rsp.
$parentConfig = Join-Path (Split-Path $Project) '.editorconfig'
if (Test-Path -LiteralPath $parentConfig) {
    Copy-Item -LiteralPath $parentConfig -Destination "$Scratch/.editorconfig"
    $response = "$Scratch/Assets/csc.rsp"
    if (Test-Path -LiteralPath $response) {
        (Get-Content -LiteralPath $response -Raw).Replace('../.editorconfig','.editorconfig') |
            Set-Content -LiteralPath $response
    }
}
# Copy resolved packages, not imported assets or compiled script assemblies.
New-Item -ItemType Directory -Force "$Scratch/Library" | Out-Null
robocopy "$Project/Library/PackageCache" "$Scratch/Library/PackageCache" /E /MT:16 /NFL /NDL /NJH /NJS /NP /R:1 /W:1
if ($LASTEXITCODE -gt 7) { throw "Package copy failed: $LASTEXITCODE" }
New-Item -ItemType Directory -Force "$Scratch/Assets/UclProjectHost/Editor" | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot/ProjectTestHost.cs" -Destination "$Scratch/Assets/UclProjectHost/ProjectTestHost.cs"
Copy-Item -LiteralPath "$PSScriptRoot/ProjectHostBuild.cs" -Destination "$Scratch/Assets/UclProjectHost/Editor/ProjectHostBuild.cs"
'{"name":"Ucl.ProjectHost","references":["UnityEngine.TestRunner"],"overrideReferences":true,"precompiledReferences":["nunit.framework.dll"]}' |
    Set-Content -LiteralPath "$Scratch/Assets/UclProjectHost/Ucl.ProjectHost.asmdef"
'{"name":"Ucl.ProjectHost.Editor","references":[],"includePlatforms":["Editor"]}' |
    Set-Content -LiteralPath "$Scratch/Assets/UclProjectHost/Editor/Ucl.ProjectHost.Editor.asmdef"
$timer.Stop()
[pscustomobject]@{cacheKey=$cacheKey;copyAndHashMs=$timer.Elapsed.TotalMilliseconds;project=$Project;scratch=$Scratch} |
    ConvertTo-Json | Set-Content -LiteralPath "$Evidence/stage.json" -Encoding utf8
Write-Output "Staged cold project. Cache key: $cacheKey"
