#Requires -Version 7.0
# Pester v5 tests for Get-VhcVbrInfo: remote runs read the DB vendor from REST serverInfo.

BeforeAll {
    . "$PSScriptRoot/../Private/Get-VhciRestDbType.ps1"
    . "$PSScriptRoot/Get-VhcVbrInfo.ps1"

    function global:Write-LogFile { param([string]$Message, [string]$LogLevel) }
    function global:Add-VhciModuleError { param([string]$CollectorName, [string]$ErrorMessage) }
    function global:Export-VhciCsv { param([Parameter(ValueFromPipeline)]$InputObject, [string]$FileName) process { $global:ExportedVbrInfo = $InputObject } }
    function global:Get-VBRBackupServerInfo { [pscustomobject]@{ Build = [version]'13.0.1.1071' } }

    $script:Cred = [pscredential]::new('veeamadmin', (ConvertTo-SecureString 'P@ss w0rd&=?' -AsPlainText -Force))
}

Describe 'Get-VhcVbrInfo DbType on remote runs' {
    BeforeEach {
        $global:ExportedVbrInfo = $null
        # No registry on a remote run.
        Mock Get-ItemProperty { $null }
    }

    It 'takes DbType from REST databaseVendor <vendor> when the registry is empty' -ForEach @(
        @{ vendor = 'PostgreSql'; expected = 'PostgreSql' }
        @{ vendor = 'MsSql';      expected = 'MsSql' }
    ) {
        Mock Invoke-RestMethod { [pscustomobject]@{ access_token = 'tok' } } -ParameterFilter { $Uri -like '*/api/oauth2/token' }
        Mock Invoke-RestMethod { [pscustomobject]@{ databaseVendor = $vendor; buildVersion = '13.0.1.1071' } } -ParameterFilter { $Uri -like '*/api/v1/serverInfo' }

        Get-VhcVbrInfo -VBRVersion 13 -RemoteExecution $true -Server 'vbr01' -Credential $script:Cred

        $global:ExportedVbrInfo.DbType | Should -Be $expected
    }

    It 'never puts the password or token in a URL' {
        Mock Invoke-RestMethod { [pscustomobject]@{ access_token = 'tok-secret' } } -ParameterFilter { $Uri -like '*/api/oauth2/token' }
        Mock Invoke-RestMethod { [pscustomobject]@{ databaseVendor = 'PostgreSql' } } -ParameterFilter { $Uri -like '*/api/v1/serverInfo' }

        Get-VhcVbrInfo -VBRVersion 13 -RemoteExecution $true -Server 'vbr01' -Credential $script:Cred

        Should -Invoke Invoke-RestMethod -ParameterFilter { $Uri -match 'P@ss|w0rd|tok-secret|password=' } -Times 0 -Exactly
        Should -Invoke Invoke-RestMethod -ParameterFilter { $Uri -like '*/api/oauth2/token' -and $Body.password -eq 'P@ss w0rd&=?' } -Times 1 -Exactly
    }

    It 'leaves DbType empty when REST is unavailable' {
        Mock Invoke-RestMethod { throw 'connection refused' }

        Get-VhcVbrInfo -VBRVersion 13 -RemoteExecution $true -Server 'vbr01' -Credential $script:Cred

        $global:ExportedVbrInfo.DbType | Should -BeNullOrEmpty
    }

    It 'does not call REST without credentials' {
        Mock Invoke-RestMethod { throw 'must not be called' }

        Get-VhcVbrInfo -VBRVersion 13 -RemoteExecution $true -Server 'vbr01'

        Should -Invoke Invoke-RestMethod -Times 0 -Exactly
        $global:ExportedVbrInfo.DbType | Should -BeNullOrEmpty
    }

    It 'keeps the registry value on a local run and does not call REST' {
        Mock Get-ItemProperty { [pscustomobject]@{ SqlActiveConfiguration = 'MsSql' } } -ParameterFilter { $Name -eq 'SqlActiveConfiguration' }
        Mock Invoke-RestMethod { throw 'must not be called' }

        Get-VhcVbrInfo -VBRVersion 13 -Server 'vbr01' -Credential $script:Cred

        $global:ExportedVbrInfo.DbType | Should -Be 'MsSql'
        Should -Invoke Invoke-RestMethod -Times 0 -Exactly
    }
}

AfterAll {
    Remove-Item function:global:Write-LogFile, function:global:Add-VhciModuleError, function:global:Export-VhciCsv, function:global:Get-VBRBackupServerInfo -ErrorAction SilentlyContinue
    Remove-Variable ExportedVbrInfo -Scope Global -ErrorAction SilentlyContinue
}
