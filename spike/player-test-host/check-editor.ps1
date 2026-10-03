param([Parameter(Mandatory=$true)][string]$Project)
$ErrorActionPreference = 'Stop'
$owned = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
    Where-Object { $_.CommandLine.Contains($Project) -or $_.CommandLine.Contains($Project.Replace('/','\')) }
if ($owned) { throw 'Spike Editor already running; do not start another.' }
