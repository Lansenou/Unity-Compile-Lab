param(
    [Parameter(Mandatory=$true)][string]$Player,
    [Parameter(Mandatory=$true)][string]$AssemblyDirectory,
    [Parameter(Mandatory=$true)][string]$Assemblies,
    [Parameter(Mandatory=$true)][string]$Cases,
    [Parameter(Mandatory=$true)][string]$Output,
    [int]$Runs = 3,
    [int]$TimeoutMs = 600000,
    [switch]$Discover,
    [switch]$Graphics
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Output | Out-Null
$measurements = @()
for ($run=1; $run -le $Runs; $run++) {
    $resultPath = Join-Path $Output "run-$run.json"
    $logPath = Join-Path $Output "run-$run.log"
    if (Test-Path -LiteralPath $resultPath) { throw "Existing results: $resultPath" }
    $arguments = @('-batchmode','-logFile',('"'+$logPath+'"'),
        '-assemblyDirectory',('"'+$AssemblyDirectory+'"'),'-assemblies',('"'+$Assemblies+'"'),
        '-cases',('"'+$Cases+'"'),'-results',('"'+$resultPath+'"'))
    if (!$Graphics) { $arguments += "-nographics" }
    if ($Discover) { $arguments += @('-discover','yes') }
    $invoked = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $Player -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $processStart = ([DateTimeOffset]$process.StartTime.ToUniversalTime()).ToUnixTimeMilliseconds()
    $timeout = !$process.WaitForExit($TimeoutMs)
    if ($timeout) { $process.Kill(); $process.WaitForExit() }
    $observedExit = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $process.Refresh()
    $report = if (Test-Path -LiteralPath $resultPath) { Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json } else { $null }
    $measurement = [pscustomobject]@{
        run=$run;pid=$process.Id;processStartUnixMs=$processStart
        invocationUnixMs=$invoked;exitUnixMs=$observedExit
        bootMs=if ($report.firstTestUnixMs) {$report.firstTestUnixMs-$processStart} else {$null}
        processToExitMs=$observedExit-$processStart
        testsMs=if ($report.finishedUnixMs -and $report.firstTestUnixMs) {$report.finishedUnixMs-$report.firstTestUnixMs} else {$null}
        exitCode=$process.ExitCode;timeout=$timeout
        total=@($report.tests).Count
        passed=@($report.tests | Where-Object outcome -EQ Passed).Count
        failed=@($report.tests | Where-Object outcome -EQ Failed).Count
        skipped=@($report.tests | Where-Object outcome -EQ Skipped).Count
        fatal=$report.fatal
    }
    $measurements += $measurement
    $measurements | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $Output 'measurements.json')
    $measurement | ConvertTo-Json -Compress | Write-Output
    if ($timeout -or $process.ExitCode -ne 0) { exit 1 }
}
