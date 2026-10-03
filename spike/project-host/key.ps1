param(
    [Parameter(Mandatory=$true)][string]$Project,
    [Parameter(Mandatory=$true)][string]$Evidence
)
$ErrorActionPreference = 'Stop'
$Project = (Resolve-Path -LiteralPath $Project).Path
New-Item -ItemType Directory -Force $Evidence | Out-Null
function TreeHash([string]$Root) {
    $entries = Get-ChildItem -LiteralPath $Root -File -Recurse | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($Root.Length).Replace('\','/')
        $relative + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($entries -join [char]10))
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}
$keyTimer = [Diagnostics.Stopwatch]::StartNew()
$key = [ordered]@{
    unity = (Get-Content -LiteralPath "$Project/ProjectSettings/ProjectVersion.txt" -Raw)
    settings = TreeHash "$Project/ProjectSettings"
    assets = TreeHash "$Project/Assets"
    packages = TreeHash "$Project/Packages"
    bootstrap = ((Get-FileHash -LiteralPath "$PSScriptRoot/ProjectTestHost.cs").Hash +
        (Get-FileHash -LiteralPath "$PSScriptRoot/ProjectHostBuild.cs").Hash).ToLowerInvariant()
    parentConfig = if (Test-Path -LiteralPath (Join-Path (Split-Path $Project) '.editorconfig')) {
        (Get-FileHash -LiteralPath (Join-Path (Split-Path $Project) '.editorconfig')).Hash.ToLowerInvariant()
    } else { '' }
    protocol = 'ucl-project-host-v1'
    target = 'StandaloneWindows64 Development Mono stripping-disabled IncludeTestAssemblies'
}
$key | ConvertTo-Json | Set-Content -LiteralPath "$Evidence/cache-key-inputs.json" -Encoding utf8
$bytes = [Text.Encoding]::UTF8.GetBytes(($key | ConvertTo-Json -Compress))
$cacheKey = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
Set-Content -LiteralPath "$Evidence/cache-key.txt" -Value $cacheKey

$keyTimer.Stop()
[pscustomobject]@{cacheKey=$cacheKey;milliseconds=$keyTimer.Elapsed.TotalMilliseconds} |
    ConvertTo-Json | Set-Content -LiteralPath "$Evidence/key-timing.json" -Encoding utf8
Write-Output "Host cache key: $cacheKey"
