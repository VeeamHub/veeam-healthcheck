#Requires -Version 7.0
# Pester tests for Invoke-SecurityComplianceDriftCheck.ps1 (per-runner SCA triage) and the
# ScaTriage.psm1 join logic it uses. Synthetic fixtures only; REST and gh are stand-ins.

BeforeAll {
    $script:DriftScript = $PSCommandPath.Replace('.Tests.ps1', '.ps1')
    Import-Module (Join-Path $PSScriptRoot 'ScaTriage.psm1') -Force

    # Stand-in for gh: the per-runner script must never call it.
    function global:gh {
        $global:VhcGhCalls.Add(($args -join ' '))
    }

    # Stand-in for REST. $global:VhcRest is a scriptblock taking ($Uri, $Headers).
    function global:Invoke-RestMethod {
        param($Uri, $Method, $Headers, $Body, $ContentType, $TimeoutSec, [switch]$SkipCertificateCheck, $ErrorAction)
        $global:VhcRestCalls.Add("$Method $Uri")
        & $global:VhcRest $Uri $Headers
    }

    $script:IdA = '11111111-1111-1111-1111-111111111111'
    $script:IdB = '22222222-2222-2222-2222-222222222222'
    $script:IdC = '33333333-3333-3333-3333-333333333333'

    function New-Fixture {
        param(
            [string]$Root,
            [object[]]$Evaluated,   # @{ Type; Id; Mapped; Source }
            [string[]]$DormantUnmapped = @(),
            [string]$Major = '13'
        )
        $dir = Join-Path $Root 'vbr-01/20261016_101104'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        $ev = foreach ($r in $Evaluated) {
            [pscustomobject][ordered]@{
                'Best Practice' = "x"; Status = 'Passed'; RuleType = $r.Type
                IsMapped = "$($r.Mapped)"; LabelSource = $(if ($r.Source) { $r.Source } else { 'JsonMap' }); RuleId = $r.Id
            }
        }
        $ev | Export-Csv -Path (Join-Path $dir 'vbr-01_SecurityCompliance.csv') -NoTypeInformation
        $cat = @(
            foreach ($r in $Evaluated) { [pscustomobject]@{ RuleType = $r.Type; IsMapped = "$($r.Mapped)"; LabelSource = 'x'; MappedLabel = 'x'; VbrVersion = $Major; ValidatedFor = '13.0.0' } }
            foreach ($d in $DormantUnmapped) { [pscustomobject]@{ RuleType = $d; IsMapped = 'False'; LabelSource = 'PascalCaseFallback'; MappedLabel = $d; VbrVersion = $Major; ValidatedFor = '13.0.0' } }
        )
        $cat | Export-Csv -Path (Join-Path $dir 'vbr-01_SecurityComplianceCatalog.csv') -NoTypeInformation
    }

    function New-Config {
        param([string]$Path)
        '{ "SecurityComplianceRulesValidatedForVbrVersion": "13.0.0", "SecurityComplianceRuleNames": { "RuleA": "Label A", "RuleB": "Label B old" } }' |
            Set-Content -Path $Path
    }

    function Set-RestHappy {
        # Console knows A (same as map), B (changed), C (not in map).
        $global:VhcRest = {
            param($Uri, $Headers)
            if ($Uri -like '*/oauth2/token') { return [pscustomobject]@{ access_token = 'tok' } }
            if ($Uri -like '*/serverInfo') { return [pscustomobject]@{ buildVersion = '13.1.1.18' } }
            return [pscustomobject]@{ items = @(
                [pscustomobject]@{ id = '11111111-1111-1111-1111-111111111111'; bestPractice = 'Label A'; status = 'Ok' }
                [pscustomobject]@{ id = '22222222-2222-2222-2222-222222222222'; bestPractice = 'Label B new'; status = 'Ok' }
                [pscustomobject]@{ id = '33333333-3333-3333-3333-333333333333'; bestPractice = 'Brand new rule'; status = 'Violation' }
            ) }
        }
    }
}

AfterAll {
    Remove-Item function:gh -ErrorAction SilentlyContinue
    Remove-Item function:Invoke-RestMethod -ErrorAction SilentlyContinue
    Remove-Variable -Name VhcGhCalls, VhcRestCalls, VhcRest -Scope Global -ErrorAction SilentlyContinue
}

Describe 'Invoke-SecurityComplianceDriftCheck' {

    BeforeEach {
        $global:VhcGhCalls = [System.Collections.Generic.List[string]]::new()
        $global:VhcRestCalls = [System.Collections.Generic.List[string]]::new()
        $global:VhcRest = { throw 'REST must not be reached' }
        $script:Root = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid())
        $script:Out = Join-Path $script:Root 'out'
        $script:Glob = Join-Path $script:Root '*/*/*_SecurityComplianceCatalog.csv'
        $script:Cfg = Join-Path $script:Root 'VbrConfig.json'
        New-Item -ItemType Directory -Path $script:Root -Force | Out-Null
        New-Config -Path $script:Cfg
        $env:SCA_REST_USER = $null; $env:SCA_REST_PASSWORD = $null; $env:SCA_REST_HOST = $null
        $script:Run = { & $script:DriftScript -CatalogPath $script:Glob -OutDir $script:Out -VbrConfigPath $script:Cfg -Server vbr-01 @args 3>&1 6>&1 | Out-String }
    }

    AfterEach {
        Remove-Item -Path $script:Root -Recurse -Force -ErrorAction SilentlyContinue
        $env:SCA_REST_USER = $null; $env:SCA_REST_PASSWORD = $null
    }

    It 'finds a host-prefixed catalog through the CI glob' {
        New-Fixture -Root $script:Root -Evaluated @(@{ Type = 'RuleA'; Id = $script:IdA; Mapped = $true })
        $out = & $script:Run
        $out | Should -Match 'Reading catalog: .*vbr-01_SecurityComplianceCatalog\.csv'
        $out | Should -Not -Match 'triage skipped'
    }

    It 'skips when the glob has no hostname wildcard, as the old CI path did' {
        New-Fixture -Root $script:Root -Evaluated @(@{ Type = 'RuleA'; Id = $script:IdA; Mapped = $true })
        $oldGlob = Join-Path $script:Root '*/*/_SecurityComplianceCatalog.csv'
        $out = & $script:DriftScript -CatalogPath $oldGlob -OutDir $script:Out -VbrConfigPath $script:Cfg 3>&1 6>&1 | Out-String
        $out | Should -Match 'triage skipped'
        Test-Path (Join-Path $script:Out 'sca-triage.json') | Should -BeFalse
    }

    It 'counts only evaluated rules: unmapped catalog enums that were never evaluated are dormant' {
        New-Fixture -Root $script:Root -Evaluated @(@{ Type = 'RuleA'; Id = $script:IdA; Mapped = $true }) -DormantUnmapped 'Dormant1', 'Dormant2'
        $out = & $script:Run
        $out | Should -Match 'no drift'
        $out | Should -Not -Match 'with drift'
        $t = Get-Content (Join-Path $script:Out 'sca-triage.json') -Raw | ConvertFrom-Json
        $t.counts.missing | Should -Be 0
        $t.counts.dormant | Should -Be 2
        @($t.dormant.enum) | Should -Contain 'Dormant1'
        @($t.evaluated | Where-Object verdict -ne 'ok') | Should -HaveCount 0
    }

    It 'triages evaluated rules without a catalog, as on VBR 12 where the SDK enum is not loadable' {
        New-Fixture -Root $script:Root -Evaluated @(
            @{ Type = 'RuleA'; Id = $script:IdA; Mapped = $true },
            @{ Type = 'RuleC'; Id = $script:IdC; Mapped = $false; Source = 'PascalCaseFallback' })
        Remove-Item (Join-Path $script:Root 'vbr-01/20261016_101104/vbr-01_SecurityComplianceCatalog.csv')
        $out = & $script:Run
        $out | Should -Not -Match 'triage skipped'
        $out | Should -Match 'No SDK catalog'
        $t = Get-Content (Join-Path $script:Out 'sca-triage.json') -Raw | ConvertFrom-Json
        $t.counts.evaluated | Should -Be 2
        $t.counts.dormant | Should -Be 0
        ($t.evaluated | Where-Object enum -eq 'RuleC').verdict | Should -Be 'missing'
    }

    It 'degrades to the CSV-only verdict with a visible notice when REST credentials are absent' {
        New-Fixture -Root $script:Root -Evaluated @(
            @{ Type = 'RuleA'; Id = $script:IdA; Mapped = $true },
            @{ Type = 'RuleC'; Id = $script:IdC; Mapped = $false; Source = 'PascalCaseFallback' })
        $out = & $script:Run
        $out | Should -Match 'REST console labels unavailable'
        $out | Should -Match 'missing RuleC'
        $t = Get-Content (Join-Path $script:Out 'sca-triage.json') -Raw | ConvertFrom-Json
        $t.rest.available | Should -BeFalse
        $t.verified | Should -BeFalse
        ($t.evaluated | Where-Object enum -eq 'RuleC').verdict | Should -Be 'missing'
        ($t.evaluated | Where-Object enum -eq 'RuleC').proposedLabel | Should -BeNullOrEmpty
        $global:VhcRestCalls | Should -HaveCount 0
    }

    It 'degrades (no failure) when the server API has no analyzer endpoint' {
        New-Fixture -Root $script:Root -Evaluated @(@{ Type = 'RuleA'; Id = $script:IdA; Mapped = $true })
        $env:SCA_REST_USER = 'svc'; $env:SCA_REST_PASSWORD = 'pw-secret'
        $global:VhcRest = {
            param($Uri, $Headers)
            if ($Uri -like '*/oauth2/token') { return [pscustomobject]@{ access_token = 'tok' } }
            if ($Uri -like '*/serverInfo') { throw [System.Net.Http.HttpRequestException]::new('nf', $null, [System.Net.HttpStatusCode]::NotFound) }
            throw [System.Net.Http.HttpRequestException]::new('nf', $null, [System.Net.HttpStatusCode]::NotFound)
        }
        $out = & $script:Run -RestHost 'vbr.example.invalid'
        $out | Should -Match 'REST console labels unavailable'
        $out | Should -Match 'HTTP 404'
        $out | Should -Not -Match 'pw-secret'
        (Get-Content (Join-Path $script:Out 'sca-triage.json') -Raw | ConvertFrom-Json).rest.available | Should -BeFalse
    }

    It 'does not retry other API versions after a rejected credential' {
        New-Fixture -Root $script:Root -Evaluated @(@{ Type = 'RuleA'; Id = $script:IdA; Mapped = $true })
        $env:SCA_REST_USER = 'svc'; $env:SCA_REST_PASSWORD = 'wrong'
        $global:VhcRest = { param($Uri, $Headers) throw [System.Net.Http.HttpRequestException]::new('no', $null, [System.Net.HttpStatusCode]::Unauthorized) }
        & $script:Run -RestHost 'vbr.example.invalid' | Out-Null
        @($global:VhcRestCalls | Where-Object { $_ -like '*oauth2/token*' }) | Should -HaveCount 1
    }

    It 'joins on the rule GUID: ok, label-changed and missing, with exact console labels' {
        New-Fixture -Root $script:Root -Evaluated @(
            @{ Type = 'RuleA'; Id = $script:IdA; Mapped = $true },
            @{ Type = 'RuleB'; Id = $script:IdB; Mapped = $true },
            @{ Type = 'RuleC'; Id = $script:IdC; Mapped = $false; Source = 'PascalCaseFallback' }) -DormantUnmapped 'Never1'
        $env:SCA_REST_USER = 'svc'; $env:SCA_REST_PASSWORD = 'pw-secret'
        Set-RestHappy
        $out = & $script:Run -RestHost 'vbr.example.invalid'
        $out | Should -Not -Match 'pw-secret'
        $t = Get-Content (Join-Path $script:Out 'sca-triage.json') -Raw | ConvertFrom-Json
        $t.schema | Should -Be 'sca-triage/1'
        $t.vbrVersion | Should -Be '13.1.1.18'
        $t.rest.available | Should -BeTrue
        $a = $t.evaluated | Where-Object enum -eq 'RuleA'
        $b = $t.evaluated | Where-Object enum -eq 'RuleB'
        $c = $t.evaluated | Where-Object enum -eq 'RuleC'
        $a.verdict | Should -Be 'ok'
        $b.verdict | Should -Be 'label-changed'
        $b.mapLabel | Should -Be 'Label B old'
        $b.proposedLabel | Should -Be 'Label B new'
        $c.verdict | Should -Be 'missing'
        $c.proposedLabel | Should -Be 'Brand new rule'
        @($t.dormant.enum) | Should -Be @('Never1')
        # No credentials or tokens in any URL.
        ($global:VhcRestCalls -join ' ') | Should -Not -Match 'pw-secret|Bearer'
    }

    It 'never calls gh' {
        New-Fixture -Root $script:Root -Evaluated @(@{ Type = 'RuleC'; Id = $script:IdC; Mapped = $false })
        & $script:Run | Out-Null
        $global:VhcGhCalls | Should -HaveCount 0
    }
}

Describe 'Get-ScaTriage verdicts' {
    It 'treats a rule whose label came from the SDK as ok' {
        $t = Get-ScaTriage -Server s -Evaluated @([pscustomobject]@{ RuleType = 'X'; IsMapped = 'True'; LabelSource = 'SdkProperty'; RuleId = '' }) `
            -LabelMap ([pscustomobject]@{}) -Rest $null
        $t.evaluated[0].verdict | Should -Be 'ok'
    }

    It 'compares labels case-insensitively' {
        $t = Get-ScaTriage -Server s -Evaluated @([pscustomobject]@{ RuleType = 'X'; IsMapped = 'True'; LabelSource = 'JsonMap'; RuleId = $script:IdA }) `
            -LabelMap ([pscustomobject]@{ X = 'Same Label' }) `
            -Rest ([pscustomobject]@{ Attempted = $true; Available = $true; Items = @([pscustomobject]@{ id = $script:IdA.ToUpper(); bestPractice = 'same label' }); Reason = 'ok'; ApiVersion = '1.2-rev0' })
        $t.evaluated[0].verdict | Should -Be 'ok'
    }
}
