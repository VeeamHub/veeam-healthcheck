#Requires -Version 7.0
# Pester v5 tests for Get-VhcVbrInfo: remote runs read the DB vendor from REST serverInfo.

BeforeAll {
    . "$PSScriptRoot/../Private/Invoke-VhciRestApi.ps1"
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
        Mock Connect-VhciRestApi { [pscustomobject]@{ BaseUrl = 'https://vbr01:9419'; Headers = @{} } }
        Mock Invoke-VhciRestApi { [pscustomobject]@{ databaseVendor = $vendor; buildVersion = '13.0.1.1071' } }

        Get-VhcVbrInfo -VBRVersion 13 -RemoteExecution $true -Server 'vbr01' -Credential $script:Cred

        $global:ExportedVbrInfo.DbType | Should -Be $expected
        Should -Invoke Invoke-VhciRestApi -ParameterFilter { $Path -eq '/api/v1/serverInfo' } -Times 1 -Exactly
    }

    It 'authenticates once through the shared client with the credential, not in a URL' {
        Mock Connect-VhciRestApi { [pscustomobject]@{ BaseUrl = 'https://vbr01:9419'; Headers = @{} } }
        Mock Invoke-VhciRestApi { [pscustomobject]@{ databaseVendor = 'PostgreSql' } }

        Get-VhcVbrInfo -VBRVersion 13 -RemoteExecution $true -Server 'vbr01' -Credential $script:Cred

        Should -Invoke Connect-VhciRestApi -Times 1 -Exactly -ParameterFilter { $Server -eq 'vbr01' -and $Credential.UserName -eq 'veeamadmin' }
        Should -Invoke Invoke-VhciRestApi -Times 0 -Exactly -ParameterFilter { $Path -match 'password|token' }
    }

    It 'makes a single auth attempt and leaves DbType empty when the login is rejected' {
        Mock Connect-VhciRestApi { throw 'auth failed' }
        Mock Invoke-VhciRestApi { throw 'must not be called' }
        Mock Write-LogFile { }

        Get-VhcVbrInfo -VBRVersion 13 -RemoteExecution $true -Server 'vbr01' -Credential $script:Cred

        Should -Invoke Connect-VhciRestApi -Times 1 -Exactly
        Should -Invoke Write-LogFile -Times 1 -Exactly -ParameterFilter { $LogLevel -eq 'WARNING' -and $Message -like '*serverInfo lookup failed*' }
        $global:ExportedVbrInfo.DbType | Should -BeNullOrEmpty
    }

    It 'leaves DbType empty and warns on an unrecognised vendor' {
        Mock Connect-VhciRestApi { [pscustomobject]@{ BaseUrl = 'x'; Headers = @{} } }
        Mock Invoke-VhciRestApi { [pscustomobject]@{ databaseVendor = 'Oracle' } }
        Mock Write-LogFile { }

        Get-VhcVbrInfo -VBRVersion 13 -RemoteExecution $true -Server 'vbr01' -Credential $script:Cred

        $global:ExportedVbrInfo.DbType | Should -BeNullOrEmpty
        Should -Invoke Write-LogFile -ParameterFilter { $LogLevel -eq 'WARNING' -and $Message -like '*not recognised*' }
    }

    It 'does not call REST without credentials' {
        Mock Connect-VhciRestApi { throw 'must not be called' }

        Get-VhcVbrInfo -VBRVersion 13 -RemoteExecution $true -Server 'vbr01'

        Should -Invoke Connect-VhciRestApi -Times 0 -Exactly
        $global:ExportedVbrInfo.DbType | Should -BeNullOrEmpty
    }

    It 'keeps the registry value on a local run and does not call REST' {
        Mock Get-ItemProperty { [pscustomobject]@{ SqlActiveConfiguration = 'MsSql' } } -ParameterFilter { $Name -eq 'SqlActiveConfiguration' }
        Mock Connect-VhciRestApi { throw 'must not be called' }

        Get-VhcVbrInfo -VBRVersion 13 -Server 'vbr01' -Credential $script:Cred

        $global:ExportedVbrInfo.DbType | Should -Be 'MsSql'
        Should -Invoke Connect-VhciRestApi -Times 0 -Exactly
    }
}

AfterAll {
    Remove-Item function:global:Write-LogFile, function:global:Add-VhciModuleError, function:global:Export-VhciCsv, function:global:Get-VBRBackupServerInfo -ErrorAction SilentlyContinue
    Remove-Variable ExportedVbrInfo -Scope Global -ErrorAction SilentlyContinue
}
