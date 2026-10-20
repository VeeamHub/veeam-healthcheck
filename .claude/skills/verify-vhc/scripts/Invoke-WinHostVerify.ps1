#Requires -Version 7
# Invoke-WinHostVerify.ps1 - runs ON the Windows host. Shipped there by `vhc-verify.sh winhost`; not meant to be run by hand.
#
# Proves, on a scratch copy of the caller's working tree:
#   build   dotnet build vHC/HC.sln
#   tests   dotnet test VhcXTests (Windows-only tests included), counts from the TRX
#   live    a real /run on the local VBR (scrub on, explicit /outdir), then the same Test-VhcLiveRun.ps1
#           checks the CI live jobs use
#   gui     VhcGuiE2ETests inside the interactive desktop session (a scheduled task with an interactive logon)
#
# Everything it starts is recorded by PID (and start time) in <Root>\work\state.json, and nothing is ever
# killed by name. -Mode Cleanup stops what is still recorded, unregisters the scheduled task and removes <Root>.
#
# VHC is started as `dotnet VeeamHealthCheck.dll`, from the build step's output, not as a published single-file exe.
# Smart App Control and similar Application Control policies block unsigned executables on many Windows 11 machines
# (verdicts vary build to build); the signed dotnet host loading a managed dll is not blocked, so the run is the same
# on every host. The single-file publish stays covered by ci-cd.yaml build-and-test.
#
# Exit codes: 0 every requested step passed; 1 a step failed; 4 --gui requested but no usable interactive session.

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Root,
    [ValidateSet('Run', 'Cleanup')][string]$Mode = 'Run',
    [string]$Steps = 'build,tests',
    [string]$LabProfile = 'vbr-13-pg',
    [string]$ExpectDbType = 'PostgreSql',
    [int]$LiveTimeoutMin = 30,
    [int]$GuiTimeoutMin = 45
)

$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath($Root)
$base = Split-Path $Root -Parent
$tree = Join-Path $Root 'tree'
$work = Join-Path $Root 'work'
$ev = Join-Path $Root 'evidence'
$stateFile = Join-Path $work 'state.json'
$activeFile = Join-Path $base 'active.json'
if ($LabProfile -eq 'none') { $LabProfile = '' }
if ($ExpectDbType -eq 'none') { $ExpectDbType = '' }
$requested = @($Steps.Split(',', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim().ToLowerInvariant() })
$results = [System.Collections.Generic.List[object]]::new()
$state = @{ pids = @(); task = '' }

function Say { param([string]$Text) Write-Host ("[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss'), $Text) }

# --- recorded processes ------------------------------------------------------
function Save-State {
    New-Item -ItemType Directory -Force -Path $work | Out-Null
    $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding utf8
}

function Read-State {
    if (Test-Path -LiteralPath $stateFile) {
        $s = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
        $script:state = @{ pids = @($s.pids); task = [string]$s.task }
    }
}

function Register-Pid {
    param([int]$ProcessId, [string]$What)
    $p = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if (-not $p) { return }
    $state.pids = @($state.pids) + [pscustomobject]@{ pid = $ProcessId; start = $p.StartTime.ToFileTimeUtc(); what = $What }
    Save-State
}

# Kills a recorded process tree by PID, only if that PID still belongs to the process we recorded (start time matches).
function Stop-Recorded {
    foreach ($r in @($state.pids)) {
        $p = Get-Process -Id $r.pid -ErrorAction SilentlyContinue
        if ($p -and $p.StartTime.ToFileTimeUtc() -eq $r.start) {
            Say "stopping recorded process $($r.pid) ($($r.what))"
            & taskkill.exe /PID $r.pid /T /F 2>&1 | Out-Null
        }
    }
    $state.pids = @()
    if (Test-Path -LiteralPath $work) { Save-State }
}

function Remove-GuiTask {
    if ($state.task) {
        $t = Get-ScheduledTask -TaskName $state.task -ErrorAction SilentlyContinue
        if ($t) {
            Stop-ScheduledTask -TaskName $state.task -ErrorAction SilentlyContinue
            Unregister-ScheduledTask -TaskName $state.task -Confirm:$false -ErrorAction SilentlyContinue
            Say "unregistered scheduled task $($state.task)"
        }
        $state.task = ''
        if (Test-Path -LiteralPath $work) { Save-State }
    }
}

function Invoke-Recorded {
    # Start a process, record its PID, redirect output to a log, enforce a timeout. -> @{ Exit; TimedOut }
    param([string]$Exe, [string]$ArgLine, [string]$WorkDir, [string]$Log, [int]$TimeoutMin, [string]$What)
    New-Item -ItemType Directory -Force -Path (Split-Path $Log -Parent) | Out-Null
    $errLog = "$Log.err"
    $p = Start-Process -FilePath $Exe -ArgumentList $ArgLine -WorkingDirectory $WorkDir -RedirectStandardOutput $Log -RedirectStandardError $errLog -NoNewWindow -PassThru
    Register-Pid -ProcessId $p.Id -What $What
    $done = $p.WaitForExit($TimeoutMin * 60000)
    if (-not $done) {
        Say "$What exceeded $TimeoutMin min; stopping recorded pid $($p.Id)"
        & taskkill.exe /PID $p.Id /T /F 2>&1 | Out-Null
        $p.WaitForExit(15000) | Out-Null
    } else {
        $p.WaitForExit()
    }
    if (Test-Path -LiteralPath $errLog) {
        $e = Get-Content -LiteralPath $errLog -Raw
        if ($e) { Add-Content -LiteralPath $Log -Value "`n--- stderr ---`n$e" }
        Remove-Item -LiteralPath $errLog -Force
    }
    $state.pids = @($state.pids | Where-Object { $_.pid -ne $p.Id })
    Save-State
    @{ Exit = $(if ($done) { $p.ExitCode } else { -1 }); TimedOut = (-not $done) }
}

function Add-Result {
    param([string]$Name, [ValidateSet('pass', 'fail', 'skip')][string]$Status, [string]$Detail, [hashtable]$Counts = @{})
    $results.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail; counts = $Counts })
    $tag = @{ pass = 'PASS'; fail = 'FAIL'; skip = 'SKIP' }[$Status]
    Say ("[{0}] {1}: {2}" -f $tag, $Name, $Detail)
}

# --- cleanup mode ------------------------------------------------------------
if ($Mode -eq 'Cleanup') {
    Read-State
    Stop-Recorded
    Remove-GuiTask
    if (Test-Path -LiteralPath $Root) {
        for ($i = 0; $i -lt 5; $i++) {
            try { Remove-Item -LiteralPath $Root -Recurse -Force -ErrorAction Stop; break } catch { Start-Sleep -Seconds 2 }
        }
    }
    if ((Test-Path -LiteralPath $activeFile) -and ((Get-Content -LiteralPath $activeFile -Raw | ConvertFrom-Json).root -eq $Root)) { Remove-Item -LiteralPath $activeFile -Force }
    if ((Test-Path -LiteralPath $base) -and -not (Get-ChildItem -LiteralPath $base -Force | Select-Object -First 1)) { Remove-Item -LiteralPath $base -Force }
    $left = Test-Path -LiteralPath $Root
    Say $(if ($left) { "cleanup incomplete: $Root still exists" } else { 'scratch removed' })
    exit $(if ($left) { 1 } else { 0 })
}

# --- interactive session probe -----------------------------------------------
function Get-InteractiveSession {
    # -> @{ Ok; SessionId; Message }. Ok only for an Active (connected, unlocked) session of the current user.
    $me = $env:USERNAME
    $raw = & query.exe user 2>&1 | ForEach-Object { "$_" }
    $mine = @()
    foreach ($line in $raw) {
        if ($line -match '^\s*>?(\S+)\s+(?:(\S+)\s+)?(\d+)\s+(Active|Disc)\b') {
            if ($Matches[1] -ieq $me) { $mine += [pscustomobject]@{ Id = [int]$Matches[3]; State = $Matches[4] } }
        }
    }
    if ($mine.Count -eq 0) {
        return @{ Ok = $false; Message = "no interactive desktop session for user '$me' (query user lists none). Sign in over RDP and leave that session connected; GUI tests need a real desktop." }
    }
    $active = @($mine | Where-Object State -eq 'Active') | Select-Object -First 1
    if (-not $active) {
        return @{ Ok = $false; Message = "the interactive session ($($mine[0].Id)) is disconnected; Windows locks the desktop on disconnect. Reconnect the RDP session and leave it connected." }
    }
    if (Get-Process -Name LogonUI -ErrorAction SilentlyContinue | Where-Object SessionId -eq $active.Id) {
        return @{ Ok = $false; Message = "the interactive session ($($active.Id)) is locked (LogonUI is running there). Unlock it and disable the screen lock; GUI tests need an unlocked desktop." }
    }
    @{ Ok = $true; SessionId = $active.Id; Message = "interactive session $($active.Id) is active and unlocked" }
}

# --- step: build -------------------------------------------------------------
function Invoke-BuildStep {
    $log = Join-Path $ev 'build.log'
    $r = Invoke-Recorded -Exe 'dotnet' -WorkDir $tree -Log $log -TimeoutMin 30 -What 'dotnet build' `
        -ArgLine 'build vHC/HC.sln --configuration Debug -nodeReuse:false -p:UseSharedCompilation=false --nologo'
    $text = Get-Content -LiteralPath $log -Raw
    $w = [regex]::Match($text, '(\d+) Warning\(s\)'); $e = [regex]::Match($text, '(\d+) Error\(s\)')
    $counts = @{ warnings = $(if ($w.Success) { [int]$w.Groups[1].Value } else { 0 }); errors = $(if ($e.Success) { [int]$e.Groups[1].Value } else { 0 }) }
    if ($r.TimedOut) { Add-Result 'build' 'fail' 'dotnet build timed out' $counts; return $false }
    if ($r.Exit -ne 0 -or $counts.errors -gt 0) { Add-Result 'build' 'fail' "dotnet build exited $($r.Exit), $($counts.errors) error(s); see build.log" $counts; return $false }
    Add-Result 'build' 'pass' "vHC/HC.sln built, $($counts.warnings) warning(s), 0 errors" $counts
    $true
}

function Get-TrxCounts {
    param([string]$Path)
    [xml]$x = Get-Content -LiteralPath $Path -Raw
    $c = $x.TestRun.ResultSummary.Counters
    $skipped = @($x.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'NotExecuted' })
    @{ total = [int]$c.total; passed = [int]$c.passed; failed = [int]$c.failed; skipped = $skipped.Count }
}

# --- step: tests -------------------------------------------------------------
function Invoke-TestsStep {
    $trxDir = Join-Path $ev 'trx'
    $r = Invoke-Recorded -Exe 'dotnet' -WorkDir $tree -Log (Join-Path $ev 'tests.log') -TimeoutMin 60 -What 'dotnet test VhcXTests' `
        -ArgLine "test vHC/VhcXTests/VhcXTests.csproj --configuration Debug --no-build -nodeReuse:false --logger `"trx;LogFileName=vhcxtests.trx`" --results-directory `"$trxDir`" --nologo"
    $trx = Join-Path $trxDir 'vhcxtests.trx'
    if (-not (Test-Path -LiteralPath $trx)) { Add-Result 'tests' 'fail' "no TRX produced (exit $($r.Exit)); see tests.log" @{}; return }
    $c = Get-TrxCounts $trx
    $detail = "VhcXTests: $($c.passed) passed, $($c.failed) failed, $($c.skipped) skipped of $($c.total)"
    if ($r.TimedOut) { Add-Result 'tests' 'fail' "timed out; $detail" $c }
    elseif ($c.failed -gt 0 -or $r.Exit -ne 0 -or $c.passed -eq 0) { Add-Result 'tests' 'fail' "$detail (exit $($r.Exit))" $c }
    else { Add-Result 'tests' 'pass' $detail $c }
}

# --- launcher: dotnet <dll> from the build output (see the header) -----------------
function Get-Launcher {
    # The build puts the dll under bin\Debug\<tfm>\<rid>\; take the newest one rather than hard-coding the runtime id.
    $dll = Get-ChildItem -Path (Join-Path $tree 'vHC\HC_Reporting\bin\Debug') -Recurse -File -Filter 'VeeamHealthCheck.dll' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $dll) { return $null }
    @{ Dotnet = (Get-Command dotnet).Source; Dll = $dll.FullName }
}

# --- step: live ----------------------------------------------------------------
function Invoke-LiveStep {
    param($Launcher)
    $liveEv = Join-Path $ev 'live'
    New-Item -ItemType Directory -Force -Path $liveEv | Out-Null
    $out = Join-Path $work 'live-out'
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    $started = Get-Date

    # Scrub on; experiments on so the gated sections render and -Check Scrub can find them (as the CI scrubbed pass does).
    $env:VHC_EXPERIMENTS = '1'
    try {
        $r = Invoke-Recorded -Exe $Launcher.Dotnet -WorkDir (Split-Path $Launcher.Dll -Parent) -Log (Join-Path $liveEv 'vhc-run.log') -TimeoutMin $LiveTimeoutMin -What 'VeeamHealthCheck /run' `
            -ArgLine "`"$($Launcher.Dll)`" /run /outdir=`"$out`" /silent /scrub:true"
    } finally { Remove-Item Env:\VHC_EXPERIMENTS -ErrorAction SilentlyContinue }
    if ($r.TimedOut) { Add-Result 'live' 'fail' "the /run did not finish within $LiveTimeoutMin min" @{}; return }
    if ($r.Exit -ne 0) { Add-Result 'live' 'fail' "VeeamHealthCheck /run exited $($r.Exit); see live/vhc-run.log" @{}; return }

    $anon = Join-Path $out 'vHC-AnonymousReport'
    $logDir = Join-Path $out 'Original\Log'
    $report = Get-ChildItem -LiteralPath $anon -Filter '*.html' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    foreach ($copy in @(@($anon, 'report'), @($logDir, 'log'))) {
        if (Test-Path -LiteralPath $copy[0]) { Copy-Item -LiteralPath $copy[0] -Destination (Join-Path $liveEv $copy[1]) -Recurse -Force }
    }

    $checkScript = Join-Path $tree '.github\scripts\Test-VhcLiveRun.ps1'
    $plan = @(
        @{ Check = 'SessionLog';    Args = @('-LogDir', $logDir) },
        @{ Check = 'CsvContent';    Args = @('-CsvRoot', (Join-Path $out 'Original\VBR')) },
        @{ Check = 'RowCounts';     Args = @('-CsvRoot', (Join-Path $out 'Original\VBR')) },
        @{ Check = 'CsvOutputs';    Args = @('-CsvRoot', (Join-Path $out 'Original\VBR'), '-RepoRoot', $tree) + $(if ($LabProfile) { @('-LabProfile', $LabProfile) } else { @() }) },
        @{ Check = 'ReportContent'; Args = @('-ReportPath', "$($report.FullName)") },
        @{ Check = 'ReportTables';  Args = @('-ReportPath', "$($report.FullName)") },
        @{ Check = 'Json';          Args = @('-ReportDir', $anon) },
        @{ Check = 'Scrub';         Args = @('-AnonDir', $anon, '-ScrubHost', $env:COMPUTERNAME, '-NewerThan', $started.AddSeconds(-5).ToString('o')) },
        # No Entra tenant on a home-lab host is the normal case: the check prints INFO and passes, and a tenant job's RetentionPolicy is only reported.
        @{ Check = 'EntraTenantJob'; Args = @('-CsvRoot', (Join-Path $out 'Original\VBR'), '-Informational') }
    )
    # Which configuration database this run collected from (vbrinfo DbType, registry value on a local run): the shared
    # DbType check, so a host on another engine, or an empty/Unknown value, fails here rather than passing quietly.
    if ($ExpectDbType) {
        $plan = @(@{ Check = 'DbType'; Args = @('-CsvRoot', (Join-Path $out 'Original\VBR'), '-ExpectedDbType', $ExpectDbType) }) + $plan
    }
    $failed = @(); $passed = 0

    foreach ($p in $plan) {
        $log = Join-Path $liveEv "check-$($p.Check).log"
        if (-not $report -and $p.Check -in 'ReportContent', 'ReportTables', 'Json', 'Scrub') {
            "no scrubbed HTML report under $anon" | Set-Content -LiteralPath $log; $failed += $p.Check
            Say "  check $($p.Check): FAIL (no report)"; continue
        }
        $argList = @('-NoProfile', '-File', $checkScript, '-Check', $p.Check) + $p.Args
        & pwsh @argList *>&1 | ForEach-Object { "$_" } | Set-Content -LiteralPath $log -Encoding utf8
        if ($LASTEXITCODE -eq 0) { $passed++; Say "  check $($p.Check): pass" } else { $failed += $p.Check; Say "  check $($p.Check): FAIL (see live/check-$($p.Check).log)" }
    }
    $counts = @{ checks_passed = $passed; checks_failed = $failed.Count; checks_skipped = 1 }
    $skipNote = 'Exports skipped: scrub mode writes no PDF or PPTX'
    if ($failed.Count -gt 0) { Add-Result 'live' 'fail' "/run ok, checks failed: $($failed -join ', '); $passed passed. $skipNote" $counts }
    else { Add-Result 'live' 'pass' "/run ok (scrub on), $passed checks passed (db $ExpectDbType, profile $(if ($LabProfile) { $LabProfile } else { 'none' })). $skipNote" $counts }
}

# --- step: gui -------------------------------------------------------------------
function Invoke-GuiStep {
    param($Launcher, [bool]$Live)
    $guiEv = Join-Path $ev 'gui'
    $guiOut = Join-Path $work 'gui-out'
    New-Item -ItemType Directory -Force -Path $guiEv, $guiOut | Out-Null
    $taskScript = Join-Path $work 'gui-task.ps1'
    $pidFile = Join-Path $work 'gui.pid'
    $exitFile = Join-Path $work 'gui.exit'
    $log = Join-Path $guiEv 'gui.log'
    Remove-Item -LiteralPath $pidFile, $exitFile -Force -ErrorAction SilentlyContinue

    # Runs inside the interactive session. dotnet test is invoked synchronously, so the task is the process tree root.
    $liveFlag = if ($Live) { '1' } else { '0' }
    @"
`$ErrorActionPreference = 'Continue'
Set-Content -LiteralPath '$pidFile' -Value `$PID
`$env:VHC_E2E_EXE = '$($Launcher.Dll)'
`$env:DOTNET_HOST_PATH = '$($Launcher.Dotnet)'
`$env:VHC_E2E_OUT = '$guiOut'
`$env:VHC_E2E_RUN_TIMEOUT_MIN = '$LiveTimeoutMin'
if ('$liveFlag' -eq '1') { `$env:VHC_E2E_LIVE = '1' }
`$env:VHC_EXPERIMENTS = '1'
`$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Set-Location -LiteralPath '$tree'
& dotnet test vHC/VhcGuiE2ETests/VhcGuiE2ETests.csproj --configuration Debug --no-build -nodeReuse:false --logger "trx;LogFileName=gui-e2e.trx" --logger "console;verbosity=detailed" --results-directory '$guiEv' *> '$log'
Set-Content -LiteralPath '$exitFile' -Value `$LASTEXITCODE
"@ | Set-Content -LiteralPath $taskScript -Encoding utf8

    $taskName = "vhc-verify-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
    $state.task = $taskName
    Save-State
    $pwsh = (Get-Command pwsh).Source
    $action = New-ScheduledTaskAction -Execute $pwsh -Argument "-NoProfile -NonInteractive -WindowStyle Hidden -File `"$taskScript`""
    $principal = New-ScheduledTaskPrincipal -UserId (& whoami.exe).Trim() -LogonType Interactive -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Minutes $GuiTimeoutMin)
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings | Out-Null
    Start-ScheduledTask -TaskName $taskName
    Say "GUI suite started in the interactive session as task $taskName (live run test: $(if ($Live) { 'on' } else { 'off' }))"

    $deadline = (Get-Date).AddMinutes($GuiTimeoutMin)
    $printed = 0
    $timedOut = $false
    $registeredPid = $false
    while (-not (Test-Path -LiteralPath $exitFile)) {
        if ((Get-Date) -gt $deadline) { $timedOut = $true; break }
        if (-not $registeredPid -and (Test-Path -LiteralPath $pidFile)) {
            Register-Pid -ProcessId ([int](Get-Content -LiteralPath $pidFile -Raw).Trim()) -What 'gui task pwsh'
            $registeredPid = $true
        }
        if (Test-Path -LiteralPath $log) {
            $lines = @(Get-Content -LiteralPath $log -ErrorAction SilentlyContinue)
            if ($lines.Count -gt $printed) { $lines[$printed..($lines.Count - 1)] | ForEach-Object { Write-Host "    | $_" }; $printed = $lines.Count }
        }
        $info = Get-ScheduledTaskInfo -TaskName $taskName -ErrorAction SilentlyContinue
        $t = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        if ($t -and $t.State -ne 'Running' -and -not (Test-Path -LiteralPath $exitFile) -and $info -and $info.LastRunTime -gt [datetime]'2000-01-01') {
            Start-Sleep -Seconds 3
            if (-not (Test-Path -LiteralPath $exitFile)) { break }
        }
        Start-Sleep -Seconds 3
    }
    if (Test-Path -LiteralPath $log) {
        $lines = @(Get-Content -LiteralPath $log -ErrorAction SilentlyContinue)
        if ($lines.Count -gt $printed) { $lines[$printed..($lines.Count - 1)] | ForEach-Object { Write-Host "    | $_" } }
    }

    $exited = Test-Path -LiteralPath $exitFile
    $guiExit = if ($exited) { [int](Get-Content -LiteralPath $exitFile -Raw).Trim() } else { -1 }
    Stop-Recorded
    Remove-GuiTask
    Get-ChildItem -LiteralPath $guiOut -Recurse -File -Filter '*.png' -ErrorAction SilentlyContinue | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $guiEv -Force }

    $trx = Join-Path $guiEv 'gui-e2e.trx'
    if ($timedOut) { Add-Result 'gui' 'fail' "the GUI suite did not finish within $GuiTimeoutMin min (task stopped)" @{}; return }
    if (-not (Test-Path -LiteralPath $trx)) { Add-Result 'gui' 'fail' "no TRX produced (task exit $guiExit); see gui/gui.log" @{}; return }
    $c = Get-TrxCounts $trx
    $detail = "VhcGuiE2ETests: $($c.passed) passed, $($c.failed) failed, $($c.skipped) skipped of $($c.total)"
    if ($c.failed -gt 0 -or $guiExit -ne 0 -or $c.passed -eq 0) { Add-Result 'gui' 'fail' "$detail (exit $guiExit)" $c }
    elseif ($c.skipped -gt 0 -and $Live) { Add-Result 'gui' 'fail' "$detail; a test was skipped although everything was enabled" $c }
    else { Add-Result 'gui' 'pass' $detail $c }
}

function Write-Summary {
    $overall = if (@($results | Where-Object status -eq 'fail').Count -gt 0) { 'fail' } else { 'pass' }
    [pscustomobject]@{ overall = $overall; finished = (Get-Date).ToString('o'); steps = @($results) } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $ev 'summary.json') -Encoding utf8
    $overall
}

# --- main ----------------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $ev, $work | Out-Null

# One verification at a time on this host.
if (Test-Path -LiteralPath $activeFile) {
    $a = Get-Content -LiteralPath $activeFile -Raw | ConvertFrom-Json
    $other = Get-Process -Id $a.pid -ErrorAction SilentlyContinue
    if ($other -and $other.StartTime.ToFileTimeUtc() -eq $a.start -and $a.root -ne $Root) {
        Write-Host "another verification is already running on this host (pid $($a.pid)); wait for it or run its cleanup."
        exit 1
    }
}
$self = Get-Process -Id $PID
[pscustomobject]@{ pid = $PID; start = $self.StartTime.ToFileTimeUtc(); root = $Root } | ConvertTo-Json | Set-Content -LiteralPath $activeFile -Encoding utf8

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$wantGui = $requested -contains 'gui'
$wantLive = $requested -contains 'live'

try {
    if ($wantGui) {
        $s = Get-InteractiveSession
        if (-not $s.Ok) {
            Write-Host "GUI-UNAVAILABLE: $($s.Message)"
            Add-Result 'gui' 'fail' $s.Message @{}
            Write-Summary | Out-Null
            exit 4
        }
        Say $s.Message
    }

    $built = $false
    try { $built = Invoke-BuildStep } catch { Add-Result 'build' 'fail' "step threw: $($_.Exception.Message)" @{} }
    if (-not ($requested -contains 'tests')) { Add-Result 'tests' 'skip' 'not requested (--no-tests)' @{} }
    elseif ($built) { try { Invoke-TestsStep } catch { Add-Result 'tests' 'fail' "step threw: $($_.Exception.Message)" @{} } }
    else { Add-Result 'tests' 'skip' 'build failed' @{} }

    $launcher = $null
    if ($wantLive -or $wantGui) {
        if ($built) { $launcher = Get-Launcher }
        $why = if (-not $built) { 'build failed' } elseif (-not $launcher) { 'VeeamHealthCheck.dll is missing from the build output' } else { '' }
    }
    # A step that throws is a failed step with the reason, never a missing summary.
    $guarded = { param([string]$Name, [scriptblock]$Body) try { & $Body } catch { Add-Result $Name 'fail' "step threw: $($_.Exception.Message)" @{} } }
    if ($wantLive) { if ($launcher) { & $guarded 'live' { Invoke-LiveStep -Launcher $launcher } } else { Add-Result 'live' 'fail' $why @{} } } else { Add-Result 'live' 'skip' 'not requested (use --live)' @{} }
    if ($wantGui) { if ($launcher) { & $guarded 'gui' { Invoke-GuiStep -Launcher $launcher -Live $wantLive } } else { Add-Result 'gui' 'fail' $why @{} } } else { Add-Result 'gui' 'skip' 'not requested (use --gui)' @{} }
} finally {
    Stop-Recorded
    Remove-GuiTask
    if (Test-Path -LiteralPath $activeFile) { Remove-Item -LiteralPath $activeFile -Force -ErrorAction SilentlyContinue }
}

$overall = Write-Summary
Say "overall: $overall"
exit $(if ($overall -eq 'pass') { 0 } else { 1 })
