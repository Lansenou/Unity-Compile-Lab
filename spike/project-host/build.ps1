param(
    [Parameter(Mandatory=$true)][string]$Editor,
    [Parameter(Mandatory=$true)][string]$Project,
    [Parameter(Mandatory=$true)][string]$Player,
    [Parameter(Mandatory=$true)][string]$Evidence,
    [string]$Label = 'cold',
    [string]$CacheKeyFile,
    [switch]$Reuse
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force (Split-Path $Player),$Evidence | Out-Null
$reuseTimer = [Diagnostics.Stopwatch]::StartNew()
$cacheKey = if ($CacheKeyFile) { (Get-Content -LiteralPath $CacheKeyFile -Raw).Trim() } else { '' }
if ($Reuse) {
    $sidecar = "$Player.cache-key"
    if (!$cacheKey -or !(Test-Path -LiteralPath $Player) -or !(Test-Path -LiteralPath $sidecar) -or
        (Get-Content -LiteralPath $sidecar -Raw).Trim() -ne $cacheKey) {
        throw 'Host cache miss. Run a guarded build first.'
    }
    $reuseTimer.Stop()
    [pscustomobject]@{label=$Label;milliseconds=$reuseTimer.Elapsed.TotalMilliseconds;exitCode=0;reused=$true;cacheKey=$cacheKey} |
        ConvertTo-Json | Set-Content -LiteralPath "$Evidence/build-$Label.json" -Encoding utf8
    Write-Output "Reused keyed host in $($reuseTimer.Elapsed.TotalMilliseconds) ms."
    exit 0
}
$arguments = @('-batchmode','-nographics','-quit','-projectPath',('"'+$Project+'"'),
    '-executeMethod','ProjectHostBuild.Build','-playerOutput',('"'+$Player+'"'),'-logFile',('"'+$Evidence+'/build-'+$Label+'.log"'))
$timer = [Diagnostics.Stopwatch]::StartNew()
$process = Start-Process -FilePath $Editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Owned Editor PID: $($process.Id)"
$process.WaitForExit()
$timer.Stop()
$process.Refresh()
[pscustomobject]@{label=$Label;milliseconds=$timer.Elapsed.TotalMilliseconds;exitCode=$process.ExitCode;pid=$process.Id;cacheKey=$cacheKey} |
    ConvertTo-Json | Set-Content -LiteralPath "$Evidence/build-$Label.json" -Encoding utf8
Write-Output "Editor exit $($process.ExitCode), wall $($timer.Elapsed.TotalSeconds) s"
if ($process.ExitCode -eq 0 -and $cacheKey) { Set-Content -LiteralPath "$Player.cache-key" -Value $cacheKey }
exit $process.ExitCode
