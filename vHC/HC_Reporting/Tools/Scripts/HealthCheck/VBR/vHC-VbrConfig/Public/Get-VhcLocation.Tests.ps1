#Requires -Version 7.0
# Pester v5 tests for Get-VhcLocation.
#
# VBR cmdlets are stubbed and mocked, so these run without a VBR server. They pin the
# contract the C# report relies on: _Locations.csv columns, one row per
# (location, assigned object), a blank-object row for an unassigned location, and no
# file / no module error when VBR has no locations or an object cannot take a location.

BeforeAll {
    foreach ($name in 'Get-VBRLocation', 'Get-VBRBackupRepository', 'Get-VBRObjectStorageRepository',
                      'Get-VBRServer', 'Get-VBRViProxy', 'Get-VBRHvProxy', 'Get-VBRRepositoryExtent') {
        if (-not (Get-Command $name -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
            Set-Item -Path "function:global:$name" -Value { param($Object, $Repository, [switch]$ScaleOut, [switch]$ErrorAction) }
        }
    }
    if (-not (Get-Command Add-VhciModuleError -ErrorAction SilentlyContinue | Where-Object { $_.CommandType -eq 'Cmdlet' })) {
        function global:Add-VhciModuleError { param([string]$CollectorName, [string]$ErrorMessage) }
    }
    # Export-VhciCsv is a plain stub (not a Mock): a Mock cannot reliably see pipeline
    # input, and the rows it receives are exactly what the test asserts on.
    function global:Export-VhciCsv {
        param([string]$FileName, [Parameter(ValueFromPipeline)]$InputObject)
        begin   { $global:VhcTestExportName = $FileName; $global:VhcTestExported = [System.Collections.Generic.List[object]]::new() }
        process { if ($null -ne $InputObject) { $global:VhcTestExported.Add($InputObject) } }
    }

    $moduleRoot = Split-Path -Parent $PSScriptRoot
    . (Join-Path $moduleRoot 'Public/Write-LogFile.ps1')
    . $PSCommandPath.Replace('.Tests.ps1', '.ps1')

    $script:ColumbusId = [guid]'11111111-1111-1111-1111-111111111111'
    $script:DublinId   = [guid]'22222222-2222-2222-2222-222222222222'
    $script:Columbus   = [pscustomobject]@{ Name = 'Columbus'; Id = $script:ColumbusId }
    $script:Dublin     = [pscustomobject]@{ Name = 'Dublin';   Id = $script:DublinId }
}

Describe 'Get-VhcLocation' {

    BeforeEach {
        Mock Write-LogFile -MockWith { }
        Mock Add-VhciModuleError -MockWith { }
        $global:VhcTestExportName = $null
        $global:VhcTestExported   = $null
        Mock Get-VBRObjectStorageRepository { @() }
        Mock Get-VBRViProxy { @() }
        Mock Get-VBRHvProxy { @() }
        Mock Get-VBRServer { @() }
        Mock Get-VBRBackupRepository { @() }
        Mock Get-VBRRepositoryExtent { @() } -RemoveParameterType Repository
    }

    It 'writes one row per assigned object and a blank-object row for an unassigned location' {
        $repo = [pscustomobject]@{ Name = 'Hardened Repo 01'; Id = [guid]::NewGuid() }
        $esx  = [pscustomobject]@{ Name = 'esx-01';           Id = [guid]::NewGuid() }
        Mock Get-VBRLocation { @($script:Columbus, $script:Dublin) } -ParameterFilter { -not $Object }
        Mock Get-VBRLocation { $script:Columbus } -ParameterFilter { $Object -and $Object.Name -in 'Hardened Repo 01', 'esx-01' }
        Mock Get-VBRBackupRepository { @($repo) } -ParameterFilter { -not $ScaleOut }
        Mock Get-VBRServer { @($esx) }

        Get-VhcLocation

        $global:VhcTestExportName | Should -Be '_Locations.csv'
        $global:VhcTestExported.Count | Should -Be 3
        $columbus = @($global:VhcTestExported | Where-Object Location -eq 'Columbus')
        $columbus.Count | Should -Be 2
        ($columbus | Where-Object ObjectName -eq 'Hardened Repo 01').ObjectType | Should -Be 'Repository'
        ($columbus | Where-Object ObjectName -eq 'esx-01').ObjectType | Should -Be 'Server'
        $dublin = $global:VhcTestExported | Where-Object Location -eq 'Dublin'
        $dublin.ObjectName | Should -Be ''
        $dublin.LocationId | Should -Be "$($script:DublinId)"
        $global:VhcTestExported[0].PSObject.Properties.Name | Should -Be @('Location', 'LocationId', 'ObjectType', 'ObjectName', 'ObjectId')
    }

    It 'labels scale-out repositories SOBR and their performance extents Extent, one row per object' {
        $sobr   = [pscustomobject]@{ Name = 'SOBR-01';  Id = [guid]::NewGuid() }
        $extRepo = [pscustomobject]@{ Name = 'Extent-A'; Id = [guid]::NewGuid() }
        Mock Get-VBRLocation { @($script:Columbus) } -ParameterFilter { -not $Object }
        Mock Get-VBRLocation { $script:Columbus } -ParameterFilter { $Object }
        # -RemoveParameterType: with the real Veeam module loaded, -Repository is typed
        # VBRScaleOutBackupRepository[] and would reject the stand-in objects.
        # VBR may list an extent's repository both as a plain repository and as an extent.
        Mock Get-VBRBackupRepository { @($extRepo) } -ParameterFilter { -not $ScaleOut }
        Mock Get-VBRBackupRepository { @($sobr) } -ParameterFilter { $ScaleOut }
        Mock Get-VBRRepositoryExtent { @([pscustomobject]@{ Repository = $extRepo }) } -RemoveParameterType Repository

        Get-VhcLocation

        $global:VhcTestExported.Count | Should -Be 2
        ($global:VhcTestExported | Where-Object ObjectName -eq 'SOBR-01').ObjectType | Should -Be 'SOBR'
        @($global:VhcTestExported | Where-Object ObjectName -eq 'Extent-A').Count | Should -Be 1
        ($global:VhcTestExported | Where-Object ObjectName -eq 'Extent-A').ObjectType | Should -Be 'Extent'
        ($global:VhcTestExported | Where-Object ObjectName -eq 'Extent-A').ObjectId | Should -Be "$($extRepo.Id)"
    }

    It 'skips objects that cannot take a location without recording an error' {
        $win = [pscustomobject]@{ Name = 'win-01'; Id = [guid]::NewGuid() }
        Mock Get-VBRLocation { @($script:Columbus) } -ParameterFilter { -not $Object }
        Mock Get-VBRLocation { throw "Type win-01 is not supported" } -ParameterFilter { $Object }
        Mock Get-VBRServer { @($win) }

        Get-VhcLocation

        $global:VhcTestExported.Count | Should -Be 1
        $global:VhcTestExported[0].ObjectName | Should -Be ''
        Should -Invoke Add-VhciModuleError -Times 0 -Exactly
    }

    It 'writes nothing when VBR has no locations' {
        Mock Get-VBRLocation { @() } -ParameterFilter { -not $Object }

        Get-VhcLocation

        $global:VhcTestExported | Should -BeNullOrEmpty
        $global:VhcTestExportName | Should -BeNullOrEmpty
        Should -Invoke Add-VhciModuleError -Times 0 -Exactly
    }

    It 'records one module error and exports nothing when listing locations fails' {
        Mock Get-VBRLocation { throw 'boom' } -ParameterFilter { -not $Object }

        Get-VhcLocation

        Should -Invoke Add-VhciModuleError -Times 1 -Exactly -ParameterFilter { $CollectorName -eq 'Location' }
        $global:VhcTestExportName | Should -BeNullOrEmpty
    }
}
