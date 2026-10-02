param(
    [Parameter(Mandatory=$true)][string]$Player,
    [Parameter(Mandatory=$true)][string]$TestAssembly,
    [Parameter(Mandatory=$true)][string]$Output,
    [int]$Runs = 3
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Output | Out-Null
$measurements = @()
for ($run = 1; $run -le $Runs; $run++) {
    $resultPath = Join-Path $Output "run-$run.json"
    $logPath = Join-Path $Output "run-$run.log"
    $start = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $arguments = @('-batchmode','-nographics','-logFile',('"' + $logPath + '"'),
        '-testAssembly',('"' + $TestAssembly + '"'),'-results',('"' + $resultPath + '"'),'-startUtc',"$start")
    $process = Start-Process -FilePath $Player -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $processStart = ([DateTimeOffset]$process.StartTime.ToUniversalTime()).ToUnixTimeMilliseconds()
    if (!$process.WaitForExit(30000)) {
        # Only terminate the process this script started.
        $process.Kill()
        $process.WaitForExit()
        throw "Player exceeded 30 seconds in run $run."
    }
    $exitObserved = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $timer.Stop()
    $process.Refresh()
    $report = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    $log = Get-Content -LiteralPath $logPath -Raw
    $measurement = [pscustomobject]@{
        run = $run
        bootToFirstTestMs = $report.firstTestUnixMs - $processStart
        invocationToFirstTestMs = $report.bootToFirstTestMs
        processToExitMs = $exitObserved - $processStart
        invocationToExitMs = [Math]::Round($timer.Elapsed.TotalMilliseconds, 3)
        processStartUnixMs = $processStart
        exitCode = $process.ExitCode
        passed = @($report.tests | Where-Object passed).Count
        total = @($report.tests).Count
        logMarker = [regex]::IsMatch($log, '(?m)^SPIKE_LOG\r?$')
        errorLogMarker = [regex]::IsMatch($log, '(?m)^SPIKE_LOG_ERROR\r?$')
        fatal = $report.fatal
    }
    $measurements += $measurement
    $measurement | ConvertTo-Json -Compress | Write-Output
    $report.tests | Format-Table name,passed,exception -Wrap | Out-String | Write-Output
}
$measurements | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output 'measurements.json')
