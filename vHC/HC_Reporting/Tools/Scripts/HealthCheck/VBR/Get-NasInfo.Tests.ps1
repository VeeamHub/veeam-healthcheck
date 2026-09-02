#Requires -Version 7.0
# Pester v5 tests for Get-NasInfo.ps1 VMC.log existence guard (ISC-1..5)

BeforeAll {
    $script:ScriptPath = Join-Path $PSScriptRoot 'Get-NasInfo.ps1'
}

Describe 'Get-NasInfo VMC.log existence guard' {

    BeforeEach {
        $script:TempPath = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString())
        New-Item -Path $script:TempPath -ItemType Directory -Force | Out-Null
    }

    AfterEach {
        if (Test-Path -LiteralPath $script:TempPath) {
            Remove-Item -LiteralPath $script:TempPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Context 'ISC-3/4: when VMC.log is absent' {

        It 'exits with no error and invokes Get-Content zero times for the VMC.log path' {
            # Default Test-Path mock returns true (covers ReportPath existence check on line 97)
            Mock Test-Path -MockWith { $true }
            # Specific override: VMC.log path (LiteralPath) returns false
            Mock Test-Path -MockWith { $false } -ParameterFilter {
                $LiteralPath -eq 'C:\ProgramData\Veeam\Backup\Utils\VMC.log'
            }
            # Get-Content should never be called for the VMC.log path - no ParameterFilter so
            # any call (positional or named) is counted. Before the fix the script calls
            # Get-Content $logsPath unconditionally; after the fix with path absent it is skipped.
            Mock Get-Content -MockWith { @() }
            # Export-Csv: suppress actual file writes for this test
            Mock Export-Csv -MockWith { }
            # New-Item: suppress directory creation side effects
            Mock New-Item -MockWith { }

            { & $script:ScriptPath -VBRServer 'TESTSRV' -VBRVersion 12 -ReportPath $script:TempPath } |
                Should -Not -Throw

            # ISC-4: Get-Content must be called 0 times total (no guard = called once, fails RED)
            Should -Invoke Get-Content -Times 0 -Exactly
        }
    }

    Context 'ISC-5: happy path - VMC.log present with NAS INFRASTRUCTURE block' {

        It 'writes a non-empty NasFileData CSV when VMC.log contains a valid NAS block' {
            # All Test-Path calls return true (VMC.log exists, ReportPath exists)
            Mock Test-Path -MockWith { $true }

            # Fixture: one =====NAS INFRASTRUCTURE==== block ending with ========
            # Lines must be >= 49 chars so .Remove(0,49) strips the timestamp prefix.
            # After stripping, the line must match "NasBackupSourceShareStats" and contain
            # at least one "key: value" pair for Export-Csv to produce a row.
            # Prefix is 49 chars: "2024-01-15 08:30:00.123 [Info] VmcStatMgr.cs 123 "
            $prefix = 'A' * 49
            Mock Get-Content -MockWith {
                @(
                    ($prefix + '=====NAS INFRASTRUCTURE===='),
                    ($prefix + 'NasBackupSourceShareStats: Name: share1, SizeGb: 10'),
                    ($prefix + '========')
                )
            } -ParameterFilter {
                $LiteralPath -eq 'C:\ProgramData\Veeam\Backup\Utils\VMC.log'
            }

            & $script:ScriptPath -VBRServer 'TESTSRV' -VBRVersion 12 -ReportPath $script:TempPath

            $csv = Join-Path $script:TempPath 'TESTSRV_NasFileData.csv'
            (Test-Path -LiteralPath $csv) | Should -BeTrue
            # Import-Csv instead of Get-Content: the Get-Content mock above has no filter-less
            # fallback and would error on this call. Require at least one data row, not just
            # a header with zero rows.
            @(Import-Csv -LiteralPath $csv).Count | Should -BeGreaterThan 0
        }
    }
}

Describe 'Get-NasInfo per-stage failure isolation (#210)' {

    BeforeEach {
        $script:TempPath = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString())
        New-Item -Path $script:TempPath -ItemType Directory -Force | Out-Null
    }

    AfterEach {
        if (Test-Path -LiteralPath $script:TempPath) {
            Remove-Item -LiteralPath $script:TempPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Context 'one export stage fails' {

        It 'still runs the later export stages and logs only the failing one' {
            Mock Test-Path -MockWith { $true }

            # Fixture populates all three export stages with at least one row each. This
            # matters: an empty array piped into Export-Csv never actually invokes the
            # downstream command (zero pipeline input means the process block never runs), so
            # a mock covering an empty stage would never be exercised and this test would pass
            # for the wrong reason.
            $prefix = 'A' * 49
            Mock Get-Content -MockWith {
                @(
                    ($prefix + '=====NAS INFRASTRUCTURE===='),
                    ($prefix + 'TotalObjectStorageSize: Name: repo1, SizeGb: 5'),
                    ($prefix + 'NasBackupSourceShareStats: Name: share1, SizeGb: 10'),
                    ($prefix + 'TotalShareSize: FileShareID: fs1, SizeGb: 20'),
                    ($prefix + '========')
                )
            } -ParameterFilter {
                $LiteralPath -eq 'C:\ProgramData\Veeam\Backup\Utils\VMC.log'
            }
            Mock New-Item -MockWith { }
            Mock Export-Csv -MockWith { }
            # Write-Error (non-terminating by default) instead of throw: this is what a real
            # Export-Csv failure (locked file, disk full) looks like, and only becomes fatal to
            # this try block because the script sets $ErrorActionPreference = 'Stop'. Using
            # throw here would pass even if that line were deleted.
            Mock Export-Csv -MockWith { Write-Error 'Disk full' } -ParameterFilter {
                $Path -like '*_NasObjectSourceStorageSize.csv'
            }

            { & $script:ScriptPath -VBRServer 'TESTSRV' -VBRVersion 12 -ReportPath $script:TempPath } |
                Should -Not -Throw

            # All three export stages are still attempted despite the first one failing.
            Should -Invoke Export-Csv -Times 3 -Exactly

            $logFile = Join-Path $script:TempPath 'CollectorNasInfo.log'
            $logText = [System.IO.File]::ReadAllText($logFile)
            $logText | Should -Match '\[ERROR\] \[Get-NasInfo\] FAILED exporting NasObjectSourceStorageSize\.csv'
            $logText | Should -Match 'Exported 1 row\(s\) to TESTSRV_NasFileData\.csv'
            $logText | Should -Match 'Exported 1 row\(s\) to TESTSRV_NasSharesize\.csv'
        }
    }

    Context 'a failure occurs before any export stage' {

        It 'is caught by the outer handler, logged, and does not abort the script' {
            Mock Test-Path -MockWith { $true }
            Mock New-Item -MockWith { }
            # Same rationale as above: non-terminating by default, promoted to fatal only by
            # $ErrorActionPreference = 'Stop' inside the script's try block.
            Mock Get-Content -MockWith { Write-Error 'Access to the path is denied' } -ParameterFilter {
                $LiteralPath -eq 'C:\ProgramData\Veeam\Backup\Utils\VMC.log'
            }

            { & $script:ScriptPath -VBRServer 'TESTSRV' -VBRVersion 12 -ReportPath $script:TempPath } |
                Should -Not -Throw

            $logFile = Join-Path $script:TempPath 'CollectorNasInfo.log'
            $logText = [System.IO.File]::ReadAllText($logFile)
            $logText | Should -Match '\[ERROR\] \[Get-NasInfo\] FAILED: Access to the path is denied'
            # finally block still runs even though the try block failed early.
            $logText | Should -Match '\[INFO\] \[Get-NasInfo\] Completed in'
        }
    }
}
