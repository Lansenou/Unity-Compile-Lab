<#
.SYNOPSIS
Records what a real, licensed Unity 6 Editor reports for the fixture corpus (docs/oracle.md). PowerShell 7
twin of oracle/record.sh and oracle/parse-log.sh. Run it only on a machine where you are licensed to run
Unity; never in hosted CI (docs/licensing.md).

.EXAMPLE
pwsh oracle/record.ps1 'C:\Program Files\Unity\Hub\Editor\6000.0.30f1\Editor\Unity.exe' 6000.0.30f1
pwsh oracle/record.ps1 $unity 6000.0.30f1 -Filter 'asmdef-*' -TimeoutSeconds 1200 -Keep
pwsh -NoProfile -File oracle/record.ps1 -ParseOnly oracle/samples/editor-errors.log

.PARAMETER Unity
The Unity executable (...\Editor\Unity.exe, .../Editor/Unity, or a Unity.app bundle).
.PARAMETER Version
Version label, e.g. 6000.0.30f1. Only manifest cells whose unityVersion equals it are recorded.
.PARAMETER Filter
Wildcard on the fixture name (-like), default '*'.
.PARAMETER OutDir
Default oracle/results/<Version>/. Receives <fixture>.json (schema ucl-oracle/1) and logs/*.log (do not
commit logs).
.PARAMETER TimeoutSeconds
Per cell, default 900.
.PARAMETER Keep
Keep the temporary project copies.
.PARAMETER ParseOnly
Parse this Editor.log and print {"assemblies":[...],"diagnostics":[...]} exactly like parse-log.sh.
.PARAMETER ProjectPath
With -ParseOnly: the project root used to make absolute paths relative (default: -projectPath in the log).
.PARAMETER Section
With -ParseOnly: 'all' (default) or 'player' (only lines between UCL-ORACLE-BEGIN and UCL-ORACLE-END).
#>
[CmdletBinding(DefaultParameterSetName = 'Record')]
param(
    [Parameter(ParameterSetName = 'Record', Mandatory, Position = 0)] [string] $Unity,
    [Parameter(ParameterSetName = 'Record', Mandatory, Position = 1)] [string] $Version,
    [Parameter(ParameterSetName = 'Record', Position = 2)] [string] $Filter = '*',
    [Parameter(ParameterSetName = 'Record', Position = 3)] [string] $OutDir,
    [Parameter(ParameterSetName = 'Record')] [int] $TimeoutSeconds = 900,
    [Parameter(ParameterSetName = 'Record')] [switch] $Keep,
    [Parameter(ParameterSetName = 'Parse', Mandatory)] [string] $ParseOnly,
    [Parameter(ParameterSetName = 'Parse')] [string] $ProjectPath = '',
    [Parameter(ParameterSetName = 'Parse')] [ValidateSet('all', 'player')] [string] $Section = 'all'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------------------------- parser

function Get-NormalizedPath([string] $p) {
    $p = $p.Replace('\', '/')
    while ($p.StartsWith('./', [StringComparison]::Ordinal)) { $p = $p.Substring(2) }
    return $p
}

function Get-RelativeLogPath([string] $p, [string] $root) {
    $p = Get-NormalizedPath $p
    $isAbs = $p.StartsWith('/', [StringComparison]::Ordinal) -or $p -cmatch '^[A-Za-z]:/'
    if (-not $isAbs -or $root -eq '') { return $p }
    $r = (Get-NormalizedPath $root).TrimEnd('/')
    $cp = $p; $cr = $r
    if ($cr -cmatch '^[A-Za-z]:/') { $cp = $cp.ToLowerInvariant(); $cr = $cr.ToLowerInvariant() }
    if ($cp.StartsWith($cr + '/', [StringComparison]::Ordinal)) { return $p.Substring($r.Length + 1) }
    # macOS: /var/folders/... is reported as /private/var/folders/...
    if ($cp.StartsWith('/private/', [StringComparison]::Ordinal) -and $cp.Substring(8).StartsWith($cr + '/', [StringComparison]::Ordinal)) {
        return $p.Substring($r.Length + 9)
    }
    return $p
}

$script:PrefixPatterns = @(
    '^[ \t]+',
    '^\[[^\]]*\]',
    '^[0-9]{4}-[0-9]{2}-[0-9]{2}[T ][0-9]{2}:[0-9]{2}:[0-9]{2}([.,][0-9]+)?(Z|[+-][0-9]{2}:?[0-9]{2})?(\|0x[0-9A-Fa-f]+)?[ |:]*',
    '^[0-9]{2}:[0-9]{2}:[0-9]{2}([.,][0-9]+)? [ |:]*'
)

function Remove-LogPrefix([string] $s) {
    while ($true) {
        $changed = $false
        foreach ($pattern in $script:PrefixPatterns) {
            $m = [regex]::Match($s, $pattern)
            if ($m.Success -and $m.Length -gt 0) { $s = $s.Substring($m.Length); $changed = $true; break }
        }
        if (-not $changed) { return $s }
    }
}

function Get-CleanMessage([string] $m) { return $m.Replace("`t", ' ').TrimEnd(' ', "`t") }

function Compare-Ordinal([string] $a, [string] $b) { return [string]::CompareOrdinal($a, $b) }

<#
.SYNOPSIS
Parses a Unity Editor.log. Returns @{ Assemblies = sorted unique names; Diagnostics = sorted unique ordered
dictionaries (id, severity, file, line, column, message) }. Same rules as oracle/parse-log.sh.
#>
function ConvertFrom-UnityLog([string] $Path, [string] $Root = '', [string] $Section = 'all') {
    $inArgs = $false; $pendingRoot = $false; $fromLog = ''
    $want = $Section -eq 'all'
    $assemblies = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $diags = [System.Collections.Generic.List[object]]::new()
    $compiler = [regex]'\(([0-9]+),([0-9]+)\): (error|warning) ([A-Za-z_][A-Za-z0-9_]*): '
    $asmdef = [regex]' \(([^()]*\.(asmdef|asmref))\)$'
    $lines = if ($Path -eq '-') { [Console]::In.ReadToEnd() -split "`n" } else { [System.IO.File]::ReadAllLines((Resolve-Path -LiteralPath $Path).ProviderPath) }
    foreach ($raw in $lines) {
        $line = $raw.TrimEnd("`r")
        if ($line -ceq 'COMMAND LINE ARGUMENTS:') { $inArgs = $true; continue }
        if ($inArgs) {
            $t = $line.TrimStart(' ', "`t")
            if ($t -eq '') { $inArgs = $false }
            elseif ($pendingRoot) { if ($fromLog -eq '') { $fromLog = $t }; $pendingRoot = $false }
            elseif ($t.ToLowerInvariant() -eq '-projectpath') { $pendingRoot = $true }
            continue
        }
        if ($Root -eq '' -and $fromLog -ne '') { $Root = $fromLog }
        $s = Remove-LogPrefix $line
        if ($s.StartsWith('UCL-ORACLE-BEGIN', [StringComparison]::Ordinal)) { if ($Section -eq 'player') { $want = $true }; continue }
        if ($s.StartsWith('UCL-ORACLE-END', [StringComparison]::Ordinal)) { if ($Section -eq 'player') { $want = $false }; continue }
        if (-not $want) { continue }
        if ($s.StartsWith('UCL-ORACLE-ASSEMBLY:', [StringComparison]::Ordinal)) {
            $n = Get-CleanMessage $s.Substring(20).TrimStart(' ', "`t")
            if ($n -ne '') { [void]$assemblies.Add($n) }
            continue
        }
        $d = $null
        $m = $compiler.Match($s)
        if ($m.Success -and $m.Index -gt 0) {
            $d = [ordered]@{
                id = $m.Groups[4].Value; severity = $m.Groups[3].Value
                file = Get-RelativeLogPath $s.Substring(0, $m.Index) $Root
                line = [int]$m.Groups[1].Value; column = [int]$m.Groups[2].Value
                message = Get-CleanMessage $s.Substring($m.Index + $m.Length)
            }
        }
        elseif ($s.Contains('ssembl')) {
            $m = $asmdef.Match($s)
            if ($m.Success -and $m.Index -gt 0) {
                $msg = Get-CleanMessage $s.Substring(0, $m.Index)
                $d = [ordered]@{
                    id = 'UNITY-ASMDEF'; severity = $(if ($msg -match 'non-existent assembly') { 'warning' } else { 'error' })
                    file = Get-RelativeLogPath $m.Groups[1].Value $Root; line = 0; column = 0; message = $msg
                }
            }
        }
        if ($null -ne $d) {
            $key = "$($d.file)`t$($d.line)`t$($d.column)`t$($d.id)`t$($d.severity)`t$($d.message)"
            if ($seen.Add($key)) { $diags.Add($d) }
        }
    }
    $diags.Sort([Comparison[object]] {
            param($a, $b)
            $c = Compare-Ordinal $a.file $b.file; if ($c) { return $c }
            $c = $a.line.CompareTo($b.line); if ($c) { return $c }
            $c = $a.column.CompareTo($b.column); if ($c) { return $c }
            $c = Compare-Ordinal $a.id $b.id; if ($c) { return $c }
            $c = Compare-Ordinal $a.severity $b.severity; if ($c) { return $c }
            return Compare-Ordinal $a.message $b.message
        })
    return @{ Assemblies = @($assemblies); Diagnostics = $diags.ToArray() }
}

function ConvertTo-JsonString([string] $s) {
    $s = $s.Replace('\', '\\').Replace('"', '\"')
    $s = [regex]::Replace($s, '[\x01-\x1f]', ' ')
    return '"' + $s + '"'
}

# Same text as parse-log.sh prints.
function Format-ParsedLog($parsed) {
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.Append("{`n  `"assemblies`": [")
    $i = 0
    foreach ($a in $parsed.Assemblies) { [void]$sb.Append($(if ($i++) { ',' } else { '' }) + "`n    " + (ConvertTo-JsonString $a)) }
    [void]$sb.Append($(if ($i) { "`n  " } else { '' }) + "],`n  `"diagnostics`": [")
    $i = 0
    foreach ($d in $parsed.Diagnostics) {
        $o = '{"id": ' + (ConvertTo-JsonString $d.id) + ', "severity": ' + (ConvertTo-JsonString $d.severity) +
        ', "file": ' + (ConvertTo-JsonString $d.file) + ', "line": ' + $d.line + ', "column": ' + $d.column +
        ', "message": ' + (ConvertTo-JsonString $d.message) + '}'
        [void]$sb.Append($(if ($i++) { ',' } else { '' }) + "`n    " + $o)
    }
    [void]$sb.Append($(if ($i) { "`n  " } else { '' }) + "]`n}`n")
    return $sb.ToString()
}

if ($PSCmdlet.ParameterSetName -eq 'Parse') {
    if ($ParseOnly -ne '-' -and -not (Test-Path -LiteralPath $ParseOnly -PathType Leaf)) { Write-Error "no such file: $ParseOnly"; exit 2 }
    [Console]::Out.Write((Format-ParsedLog (ConvertFrom-UnityLog -Path $ParseOnly -Root $ProjectPath -Section $Section)))
    exit 0
}

# -------------------------------------------------------------------------------------------- recorder

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).ProviderPath
if (-not $OutDir) { $OutDir = Join-Path $root "oracle/results/$Version" }
$OutDir = [System.IO.Path]::GetFullPath($OutDir)
$manifest = Get-Content -Raw (Join-Path $root 'fixtures/manifest.json') | ConvertFrom-Json
if ($Unity -match '\.app[\\/]?$') { $Unity = Join-Path $Unity 'Contents/MacOS/Unity' }
if (-not (Test-Path -LiteralPath $Unity -PathType Leaf)) { Write-Error "not a file: $Unity"; exit 2 }
$hostOs = if ($IsWindows) { 'windows' } elseif ($IsMacOS) { 'macos' } elseif ($IsLinux) { 'linux' } else { 'unknown' }
$buildTargets = @{
    StandaloneWindows64 = 'Win64'; StandaloneOSX = 'OSXUniversal'; StandaloneLinux64 = 'Linux64'
    iOS = 'iOS'; Android = 'Android'; WebGL = 'WebGL'
}

# Assembly name and defines of a Bee compiler response file.
function Read-RspDefines([string] $file) {
    $name = ''; $defines = [System.Collections.Generic.List[string]]::new()
    foreach ($l in [System.IO.File]::ReadAllLines($file)) {
        $l = $l.TrimEnd("`r")
        if ($l -match '^[-/]out:(.*)$') {
            $o = $Matches[1].Replace('"', '').Replace('\', '/'); $o = $o.Substring($o.LastIndexOf('/') + 1)
            $name = $o -replace '\.dll$', ''
        }
        elseif ($l -match '^[-/](define|d):(.*)$') {
            foreach ($p in $Matches[2].Replace('"', '').Split([char[]]';,')) { if ($p) { $defines.Add($p) } }
        }
    }
    return @{ Name = $name; Defines = $defines }
}

function Invoke-Unity([string[]] $arguments) {
    $psi = [System.Diagnostics.ProcessStartInfo]::new($Unity)
    foreach ($a in $arguments) { $psi.ArgumentList.Add($a) }
    $psi.UseShellExecute = $false
    $p = [System.Diagnostics.Process]::Start($psi)
    if ($p.WaitForExit($TimeoutSeconds * 1000)) { return @{ Exit = $p.ExitCode; TimedOut = $false } }
    try { $p.Kill($true) } catch { Write-Verbose "kill: $_" }
    [void]$p.WaitForExit(30000)
    return @{ Exit = -1; TimedOut = $true }
}

Write-Host '== building fixture DLLs (tests/Ucl.StubBuilder)'
Push-Location $root
try { dotnet run --project tests/Ucl.StubBuilder -- artifacts/stubs | Out-Null; if ($LASTEXITCODE) { throw 'StubBuilder failed' } }
finally { Pop-Location }
$dlls = Join-Path $root 'artifacts/stubs/dlls'
$recorded = 0

foreach ($fixture in $manifest.fixtures) {
    if ($fixture.name -notlike $Filter) { continue }
    $cells = [System.Collections.Generic.List[object]]::new()
    foreach ($cell in @($fixture.cells | Where-Object unityVersion -EQ $Version)) {
        $extra = @(if ($cell.PSObject.Properties['extraArgs']) { $cell.extraArgs | Where-Object { $_ } })
        $label = (@($fixture.name, $cell.target, $cell.platform) + $extra) -join ' '
        $bt = $buildTargets[$cell.platform]
        if (-not $bt) { Write-Warning "skip ${label}: unknown platform"; continue }
        $unsupported = @($extra | Where-Object { $_ -ne '--development' })
        if ($unsupported.Count) { Write-Warning "skip ${label}: no Unity equivalent for $($unsupported -join ' ')"; continue }
        $development = $extra -contains '--development'

        $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('ucl-oracle.' + [System.IO.Path]::GetRandomFileName())
        $project = Join-Path $tmp 'project'
        New-Item -ItemType Directory -Force $tmp | Out-Null
        Copy-Item -Recurse (Join-Path $root "fixtures/$($fixture.name)") $project
        foreach ($m in @(if ($fixture.PSObject.Properties['materialize']) { $fixture.materialize | Where-Object { $_ } })) {
            $to = Join-Path $project $m.to
            New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
            Copy-Item (Join-Path $dlls "$($m.dll).dll") $to -Force
        }
        $log = Join-Path $tmp 'editor.log'
        $arguments = @('-batchmode', '-nographics', '-quit', '-projectPath', $project, '-buildTarget', $bt, '-logFile', $log)
        if ($cell.target -eq 'player') {
            New-Item -ItemType Directory -Force (Join-Path $project 'Packages') | Out-Null
            Copy-Item -Recurse (Join-Path $root 'oracle/package/com.ucl.oracle') (Join-Path $project 'Packages/com.ucl.oracle')
            $arguments += @('-executeMethod', 'Ucl.Oracle.CompilePlayer', '-uclOutDir', (Join-Path $tmp 'player-out'))
            if ($development) { $arguments += '-uclDevelopment' }
        }

        Write-Host "== $label ($Version)"
        $run = Invoke-Unity $arguments
        if (-not (Test-Path $log)) { New-Item -ItemType File $log | Out-Null }
        $safe = (@($fixture.name, $cell.target, $cell.platform) + @($extra | ForEach-Object { $_.TrimStart('-') })) -join '.'
        $safe = $safe -replace '[^A-Za-z0-9._-]', '_'
        New-Item -ItemType Directory -Force (Join-Path $OutDir 'logs') | Out-Null
        Copy-Item $log (Join-Path $OutDir "logs/$safe.log") -Force
        $text = [System.IO.File]::ReadAllText($log)

        $all = ConvertFrom-UnityLog -Path $log -Root $project
        $compileErrors = ($text -match '(?m)^Scripts have compiler errors') -or @($all.Diagnostics | Where-Object severity -EQ 'error').Count -gt 0
        $parsed = $all
        $rspFiles = @()
        if ($run.TimedOut) { $status = 'timeout' }
        elseif ($cell.target -eq 'player') {
            if ($text.Contains('UCL-ORACLE-END')) {
                $status = 'ok'
                $parsed = ConvertFrom-UnityLog -Path $log -Root $project -Section player
                $rspFiles = @([regex]::Matches($text, 'UCL-ORACLE-RSP: ([^\r\n]+)') | ForEach-Object { Join-Path $project $_.Groups[1].Value.Trim() } |
                    Sort-Object -Unique | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
            }
            elseif ($compileErrors) { $status = 'editor-compile-failed' }
            else { $status = 'unity-error' }
        }
        else {
            $status = if ($run.Exit -eq 0 -or $compileErrors) { 'ok' } else { 'unity-error' }
            $bee = Join-Path $project 'Library/Bee/artifacts'
            if ($status -eq 'ok' -and (Test-Path $bee)) { $rspFiles = @(Get-ChildItem -Recurse -File -Filter '*.rsp' $bee | ForEach-Object FullName) }
        }

        # assemblies: names printed by CompilePlayer plus every assembly a Bee response file was written for.
        $defs = [System.Collections.Generic.SortedDictionary[string, object]]::new([StringComparer]::Ordinal)
        foreach ($a in $parsed.Assemblies) { if (-not $defs.ContainsKey($a)) { $defs[$a] = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal) } }
        foreach ($f in $rspFiles) {
            $r = Read-RspDefines $f
            if (-not $r.Name) { continue }
            if (-not $defs.ContainsKey($r.Name)) { $defs[$r.Name] = [System.Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal) }
            foreach ($d in $r.Defines) { [void]$defs[$r.Name].Add($d) }
        }
        $result = [ordered]@{
            target = $cell.target; platform = $cell.platform; extraArgs = @($extra); status = $status; unityExitCode = $run.Exit
        }
        if ($cell.target -eq 'editor') { $result.editorOs = $hostOs }
        if ($defs.Count) {
            $result.assemblies = @($defs.GetEnumerator() | ForEach-Object {
                    $o = [ordered]@{ name = $_.Key }
                    if ($_.Value.Count) { $o.defines = @($_.Value) }
                    $o
                })
        }
        $result.diagnostics = @($parsed.Diagnostics)
        $cells.Add($result)
        Write-Host "   $status, $(@($parsed.Diagnostics).Count) diagnostic(s), log: $(Join-Path $OutDir "logs/$safe.log")"
        if ($Keep) { Write-Host "   kept $tmp" } else { Remove-Item -Recurse -Force $tmp }
    }
    if ($cells.Count -eq 0) { continue }
    $doc = [ordered]@{ schema = 'ucl-oracle/1'; unityVersion = $Version; fixture = $fixture.name; cells = $cells.ToArray() }
    $json = ($doc | ConvertTo-Json -Depth 10) -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText((Join-Path $OutDir "$($fixture.name).json"), $json + "`n")
    $recorded++
}

Write-Host "== recorded $recorded fixture(s) into $OutDir"
if ($recorded -eq 0) { Write-Error "no manifest cell matched version '$Version' and filter '$Filter'"; exit 1 }
