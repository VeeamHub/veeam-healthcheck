#Requires -Version 7.0
# Pester v5 tests for Get-VhcEntraId: each Entra cmdlet is isolated so one
# failure (e.g. the SDK wrapping a broken NAS job while it enumerates jobs)
# does not discard the data the other cmdlets returned.

BeforeAll {
    function global:Get-VBREntraIDTenant { param() }
    function global:Get-VBREntraIDLogsBackupJob { param() }
    function global:Get-VBREntraIDTenantBackupJob { param() }
    function global:Add-VhciModuleError { param([string]$CollectorName, [string]$ErrorMessage) }

    $publicDir = Split-Path -Parent $PSCommandPath
    . (Join-Path $publicDir 'Write-LogFile.ps1')
    . (Join-Path (Join-Path (Split-Path -Parent $publicDir) 'Private') 'Export-VhciCsv.ps1')
    . $PSCommandPath.Replace('.Tests.ps1', '.ps1')
    $script:BrokenMsg = "Failed to wrap nas backup job [5cc08fe5-09b8-4b7d-a52c-c37605d224d6]: Backup repository '280cd331-b099-4617-b59b-7488ad8d6046' was not found"
}

Describe 'Get-VhcEntraId' {

    BeforeEach {
        $script:Exports = @{}
        $script:Logs    = [System.Collections.Generic.List[object]]::new()
        Mock Write-LogFile { param($Message, $LogLevel) $script:Logs.Add([pscustomobject]@{ Msg = $Message; Level = $LogLevel }) }
        Mock Export-VhciCsv { param($FileName, [Parameter(ValueFromPipeline)]$InputObject) if (-not $script:Exports.ContainsKey($FileName)) { $script:Exports[$FileName] = @() }; if ($null -ne $InputObject) { $script:Exports[$FileName] += $InputObject } }
        Mock Add-VhciModuleError { }
        Mock Get-VBREntraIDTenant { [pscustomobject]@{ Name = 'contoso'; cacherepository = [pscustomobject]@{ name = 'cache' } } }
        Mock Get-VBREntraIDLogsBackupJob {
            [pscustomobject]@{ Name = 'LogJob'; BackupObject = [pscustomobject]@{ Tenant = [pscustomobject]@{ Name = 'contoso' } }
                ShortTermBackupRepository = [pscustomobject]@{ Name = 'r' }; ShortTermRetentionPeriod = 7; EnableCopyMode = $false; SecondaryTarget = $null }
        }
        Mock Get-VBREntraIDTenantBackupJob { [pscustomobject]@{ Tenant = [pscustomobject]@{ Name = 'contoso' }; RetentionPolicy = 14 } }
    }

    It 'all healthy: writes all three CSVs, no errors' {
        Get-VhcEntraId
        @($script:Exports['_entraTenants.csv']).Count | Should -Be 1
        @($script:Exports['_entraLogJob.csv']).Count | Should -Be 1
        @($script:Exports['_entraTenantJob.csv']).Count | Should -Be 1
        Should -Invoke Add-VhciModuleError -Times 0 -Exactly
    }

    It 'log-job cmdlet failing on a broken NAS job still yields tenant and tenant-job data' {
        Mock Get-VBREntraIDLogsBackupJob { throw $script:BrokenMsg }
        { Get-VhcEntraId } | Should -Not -Throw
        @($script:Exports['_entraTenants.csv']).Count | Should -Be 1
        @($script:Exports['_entraTenantJob.csv']).Count | Should -Be 1
        @($script:Logs | Where-Object { $_.Level -eq 'ERROR' -and $_.Msg -match 'Get-VBREntraIDLogsBackupJob' -and $_.Msg -match '280cd331' }).Count | Should -Be 1
        Should -Invoke Add-VhciModuleError -Times 1 -Exactly
    }

    It 'tenant-job cmdlet failing still yields tenant and log-job data' {
        Mock Get-VBREntraIDTenantBackupJob { throw $script:BrokenMsg }
        Get-VhcEntraId
        @($script:Exports['_entraTenants.csv']).Count | Should -Be 1
        @($script:Exports['_entraLogJob.csv']).Count | Should -Be 1
    }

    It 'no Entra support (tenant cmdlet throws): one error, other cmdlets not called' {
        Mock Get-VBREntraIDTenant { throw "The term 'Get-VBREntraIDTenant' is not recognized" }
        Get-VhcEntraId
        Should -Invoke Get-VBREntraIDLogsBackupJob -Times 0 -Exactly
        Should -Invoke Get-VBREntraIDTenantBackupJob -Times 0 -Exactly
        @($script:Logs | Where-Object Level -eq 'ERROR').Count | Should -Be 1
        Should -Invoke Add-VhciModuleError -Times 1 -Exactly
    }
}
