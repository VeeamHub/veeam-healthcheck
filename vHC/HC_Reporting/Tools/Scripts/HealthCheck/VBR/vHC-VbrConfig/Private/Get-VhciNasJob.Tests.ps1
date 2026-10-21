#Requires -Version 7.0
# Pester v5 tests for Get-VhciNasJob and the missing-repository tolerance helpers.
#
# Background: the lab VBR server had one repository deleted out from under existing
# objects. Veeam then throws "Unable to find repository id <guid>" while listing, and
# because the collectors run with $ErrorActionPreference = 'Stop', that one object threw
# away _nasBackup.csv and _nasBCJ.csv entirely. A customer should not have to repair their
# estate for vHC to work: the broken object is skipped (WARNING, never ERROR), everything
# else is collected, and the CSVs are still written. A real failure still fails.
#
# Convention follows Get-VhcJob.Tests.ps1: stub the Veeam cmdlets, dot-source the
# production logger and helpers, mock Write-LogFile to capture log levels.

BeforeAll {
    foreach ($stub in @('Get-VBRUnstructuredBackupJob', 'Get-VBRNASBackupCopyJob')) {
        if (-not (Get-Command $stub -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
            Set-Item -Path "function:global:$stub" -Value { [CmdletBinding()] param([string]$Name) }
        }
    }
    if (-not (Get-Command Get-VBRUnstructuredBackup -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Get-VBRUnstructuredBackup { [CmdletBinding()] param() }
    }
    if (-not (Get-Command Get-VBRUnstructuredBackupRestorePoint -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Get-VBRUnstructuredBackupRestorePoint { [CmdletBinding()] param($Backup) }
    }
    if (-not (Get-Command Export-VhciCsv -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Export-VhciCsv { param([Parameter(ValueFromPipeline = $true)]$InputObject, [string]$FileName) process {} }
    }

    $moduleRoot = Split-Path -Parent $PSScriptRoot
    . (Join-Path $moduleRoot 'Public/Write-LogFile.ps1')
    . (Join-Path $moduleRoot 'Private/Get-VhciMissingRepositoryId.ps1')
    . (Join-Path $moduleRoot 'Private/Register-VhciBrokenRepositoryReference.ps1')
    . (Join-Path $moduleRoot 'Private/Invoke-VhciTolerantEnumeration.ps1')
    . $PSCommandPath.Replace('.Tests.ps1', '.ps1')

    function script:New-NasJob {
        param([string]$Name, [string]$JobType = 'NasBackup')
        [PSCustomObject]@{ Name = $Name; JobType = $JobType; Id = [guid]::NewGuid() }
    }
}

Describe 'Get-VhciMissingRepositoryId' {
    It 'extracts the repository id from the Veeam message' {
        Get-VhciMissingRepositoryId -Message 'Unable to find repository id 280cd331-b099-4617-b59b-7488ad8d6046' |
            Should -Be '280cd331-b099-4617-b59b-7488ad8d6046'
    }

    It 'extracts it when the message carries extra text around it' {
        Get-VhciMissingRepositoryId -Message 'Job failed. Unable to find repository id 280cd331-b099-4617-b59b-7488ad8d6046.' |
            Should -Be '280cd331-b099-4617-b59b-7488ad8d6046'
    }

    It 'returns nothing for any other error' {
        Get-VhciMissingRepositoryId -Message 'The RPC server is unavailable' | Should -BeNullOrEmpty
        Get-VhciMissingRepositoryId -Message '' | Should -BeNullOrEmpty
        Get-VhciMissingRepositoryId -Message $null | Should -BeNullOrEmpty
    }
}

Describe 'Get-VhciNasJob: a NAS job references a repository that no longer exists' {

    BeforeEach {
        $script:Exported = @{}
        $script:BrokenRepositoryReferences = [System.Collections.Generic.List[PSCustomObject]]::new()
        $script:MissingRepoMessage = 'Unable to find repository id 280cd331-b099-4617-b59b-7488ad8d6046'

        $script:JobA   = script:New-NasJob -Name 'NasA'
        $script:JobBad = script:New-NasJob -Name 'NasBad'
        $script:JobC   = script:New-NasJob -Name 'NasC'

        Mock Write-LogFile -MockWith { }
        Mock Export-VhciCsv -MockWith {
            if (-not $script:Exported.ContainsKey($FileName)) { $script:Exported[$FileName] = @() }
            if ($null -ne $InputObject) { $script:Exported[$FileName] += @($InputObject) }
        }
        Mock Get-VBRUnstructuredBackup -MockWith { @() }
        Mock Get-VBRUnstructuredBackupRestorePoint -MockWith { @() }
        Mock Get-VBRNASBackupCopyJob -MockWith { @() }
    }

    It 'reads the remaining jobs one by one when Veeam aborts the listing, and writes the CSV' {
        Mock Get-VBRUnstructuredBackupJob -MockWith {
            if ($Name) {
                if ($Name -eq 'NasBad') { throw $script:MissingRepoMessage }
                return @($script:JobA, $script:JobC) | Where-Object { $_.Name -eq $Name }
            }
            $script:JobA
            throw $script:MissingRepoMessage
        }

        { Get-VhciNasJob -Jobs @($script:JobA, $script:JobBad, $script:JobC) } | Should -Not -Throw

        $script:Exported.ContainsKey('_nasBackup.csv') | Should -BeTrue
        @($script:Exported['_nasBackup.csv'].Name | Sort-Object) | Should -Be @('NasA', 'NasC')
    }

    It 'logs a WARNING naming the job and the repository id, never an ERROR' {
        Mock Get-VBRUnstructuredBackupJob -MockWith {
            if ($Name) {
                if ($Name -eq 'NasBad') { throw $script:MissingRepoMessage }
                return @($script:JobA, $script:JobC) | Where-Object { $_.Name -eq $Name }
            }
            $script:JobA
            throw $script:MissingRepoMessage
        }

        Get-VhciNasJob -Jobs @($script:JobA, $script:JobBad, $script:JobC)

        Should -Invoke Write-LogFile -ParameterFilter {
            $LogLevel -eq 'WARNING' -and $Message -match "NAS backup job 'NasBad'" -and $Message -match '280cd331-b099-4617-b59b-7488ad8d6046'
        }
        Should -Invoke Write-LogFile -Times 0 -ParameterFilter { $LogLevel -eq 'ERROR' }
        $Entry = @($script:BrokenRepositoryReferences | Where-Object { $_.ObjectName -eq 'NasBad' })
        $Entry | Should -HaveCount 1
        $Entry[0].RepositoryId | Should -Be '280cd331-b099-4617-b59b-7488ad8d6046'
    }

    It 'keeps every other job when Veeam reports the bad one as a non-terminating error' {
        Mock Get-VBRUnstructuredBackupJob -MockWith {
            $script:JobA
            Write-Error -Message $script:MissingRepoMessage -TargetObject $script:JobBad -ErrorAction Continue
            $script:JobC
        }

        { Get-VhciNasJob } | Should -Not -Throw

        @($script:Exported['_nasBackup.csv'].Name | Sort-Object) | Should -Be @('NasA', 'NasC')
        @($script:BrokenRepositoryReferences | Where-Object { $_.ObjectName -eq 'NasBad' }) | Should -HaveCount 1
        Should -Invoke Write-LogFile -Times 0 -ParameterFilter { $LogLevel -eq 'ERROR' }
    }

    It 'keeps the jobs read before the abort when no job names are known' {
        Mock Get-VBRUnstructuredBackupJob -MockWith {
            $script:JobA
            throw $script:MissingRepoMessage
        }

        { Get-VhciNasJob } | Should -Not -Throw

        @($script:Exported['_nasBackup.csv'].Name) | Should -Be @('NasA')
        Should -Invoke Write-LogFile -ParameterFilter { $LogLevel -eq 'WARNING' -and $Message -match '280cd331' }
    }

    It 'still writes _nasBCJ.csv when a backup copy job is the broken one' {
        Mock Get-VBRUnstructuredBackupJob -MockWith { @($script:JobA) }
        $CopyGood = script:New-NasJob -Name 'CopyGood' -JobType 'NasBackupCopy'
        $CopyBad  = script:New-NasJob -Name 'CopyBad'  -JobType 'NasBackupCopy'
        Mock Get-VBRNASBackupCopyJob -MockWith {
            if ($Name) {
                if ($Name -eq 'CopyBad') { throw $script:MissingRepoMessage }
                return $CopyGood
            }
            throw $script:MissingRepoMessage
        }

        { Get-VhciNasJob -Jobs @($script:JobA, $CopyGood, $CopyBad) } | Should -Not -Throw

        @($script:Exported['_nasBackup.csv'].Name) | Should -Be @('NasA')
        @($script:Exported['_nasBCJ.csv'].Name)    | Should -Be @('CopyGood')
        Should -Invoke Write-LogFile -ParameterFilter { $LogLevel -eq 'WARNING' -and $Message -match "NAS backup copy job 'CopyBad'" }
    }

    It 'leaves the size blank, not zero, for a backup whose restore points cannot be read' {
        Mock Get-VBRUnstructuredBackupJob -MockWith { @($script:JobA, $script:JobBad) }
        Mock Get-VBRUnstructuredBackup -MockWith { @($script:JobA, $script:JobBad) }
        Mock Get-VBRUnstructuredBackupRestorePoint -MockWith {
            if ($Backup.Name -eq 'NasBad') { throw $script:MissingRepoMessage }
            @()
        }

        { Get-VhciNasJob } | Should -Not -Throw

        $Rows = @($script:Exported['_nasBackup.csv'])
        $Rows | Should -HaveCount 2
        ($Rows | Where-Object { $_.Name -eq 'NasA' }).OnDiskGB   | Should -Be 0
        ($Rows | Where-Object { $_.Name -eq 'NasBad' }).OnDiskGB | Should -BeNullOrEmpty
        ($Rows | Where-Object { $_.Name -eq 'NasBad' }).SourceGB | Should -BeNullOrEmpty
        Should -Invoke Write-LogFile -ParameterFilter { $LogLevel -eq 'WARNING' -and $Message -match "NAS backup 'NasBad'" -and $Message -match '280cd331' }
        Should -Invoke Write-LogFile -Times 0 -ParameterFilter { $LogLevel -eq 'ERROR' }
    }

    It 'still fails on an error that is not a missing repository' {
        Mock Get-VBRUnstructuredBackupJob -MockWith { throw 'The RPC server is unavailable' }

        { Get-VhciNasJob } | Should -Throw '*RPC server is unavailable*'
        @($script:BrokenRepositoryReferences) | Should -HaveCount 0
    }
}

Describe 'Export-VhcBrokenRepositoryReferences' {

    BeforeAll {
        . (Join-Path (Split-Path -Parent $PSScriptRoot) 'Public/Export-VhcBrokenRepositoryReferences.ps1')
        if (-not (Get-Command Add-VhciModuleError -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
            function global:Add-VhciModuleError { param([string]$CollectorName, [string]$ErrorMessage) }
        }
    }

    BeforeEach {
        $script:Exported = @{}
        Mock Write-LogFile -MockWith { }
        Mock Export-VhciCsv -MockWith {
            if (-not $script:Exported.ContainsKey($FileName)) { $script:Exported[$FileName] = @() }
            if ($null -ne $InputObject) { $script:Exported[$FileName] += @($InputObject) }
        }
    }

    It 'writes one row per registered reference, de-duplicated' {
        $script:BrokenRepositoryReferences = [System.Collections.Generic.List[PSCustomObject]]::new()
        Register-VhciBrokenRepositoryReference -ObjectType 'Backup' -ObjectName 'B1' -RepositoryId 'aaaaaaaa-0000-0000-0000-000000000001' -Collector 'Jobs'
        Register-VhciBrokenRepositoryReference -ObjectType 'Backup' -ObjectName 'B1' -RepositoryId 'aaaaaaaa-0000-0000-0000-000000000001' -Collector 'Jobs'
        Register-VhciBrokenRepositoryReference -ObjectType 'NAS backup job' -ObjectName 'N1' -RepositoryId 'aaaaaaaa-0000-0000-0000-000000000001' -Collector 'NAS'

        Export-VhcBrokenRepositoryReferences

        $Rows = @($script:Exported['_BrokenRepositoryReferences.csv'])
        $Rows | Should -HaveCount 2
        $Rows[0].PSObject.Properties.Name | Should -Be @('ObjectType', 'ObjectName', 'RepositoryId', 'Collector')
    }

    It 'writes nothing when no reference is broken' {
        $script:BrokenRepositoryReferences = [System.Collections.Generic.List[PSCustomObject]]::new()

        Export-VhcBrokenRepositoryReferences

        $script:Exported.ContainsKey('_BrokenRepositoryReferences.csv') | Should -BeFalse
    }
}
