#Requires -Version 7.0
# Pester tests for Invoke-SecurityComplianceDriftCheck.ps1

BeforeAll {
    $script:DriftScript = $PSCommandPath.Replace('.Tests.ps1', '.ps1')

    # Stand-in for the gh CLI: records every call instead of touching GitHub.
    function global:gh {
        $global:VhcGhCalls.Add(($args -join ' '))
        if ($args[0] -eq 'issue' -and $args[1] -eq 'list') { return '[]' }
    }

    function New-Catalog {
        param([string]$Root, [string]$FileName, [bool[]]$Mapped)
        $dir = Join-Path $Root 'vbr-01/20261016_101104'
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        $i = 0
        $rows = foreach ($m in $Mapped) {
            $i++
            [pscustomobject]@{
                RuleType = "Rule$i"; IsMapped = "$m"; LabelSource = 'Fallback'; MappedLabel = "Rule $i"
                VbrVersion = '13.1.1.18'; ValidatedFor = '13.1.0'
            }
        }
        $rows | Export-Csv -Path (Join-Path $dir $FileName) -NoTypeInformation
    }
}

AfterAll {
    Remove-Item function:global:gh -ErrorAction SilentlyContinue
    Remove-Variable -Name VhcGhCalls -Scope Global -ErrorAction SilentlyContinue
}

Describe 'Invoke-SecurityComplianceDriftCheck' {

    BeforeEach {
        $global:VhcGhCalls = [System.Collections.Generic.List[string]]::new()
        $script:Root = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid())
        $script:Glob = Join-Path $script:Root '*/*/*_SecurityComplianceCatalog.csv'
        $script:Common = @{ Repo = 'org/repo'; RunUrl = 'https://example.invalid/run/1' }
    }

    AfterEach {
        Remove-Item -Path $script:Root -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'finds a host-prefixed catalog through the CI glob' {
        New-Catalog -Root $script:Root -FileName 'vbr-01_SecurityComplianceCatalog.csv' -Mapped $true
        $out = & $script:DriftScript -CatalogPath $script:Glob @Common -ReportOnly 6>&1 | Out-String
        $out | Should -Match 'Reading catalog: .*vbr-01_SecurityComplianceCatalog\.csv'
        $out | Should -Not -Match 'drift check skipped'
    }

    It 'skips when the glob has no hostname wildcard, as the old CI path did' {
        New-Catalog -Root $script:Root -FileName 'vbr-01_SecurityComplianceCatalog.csv' -Mapped $true
        $oldGlob = Join-Path $script:Root '*/*/_SecurityComplianceCatalog.csv'
        $out = & $script:DriftScript -CatalogPath $oldGlob @Common -ReportOnly 3>&1 6>&1 | Out-String
        $out | Should -Match 'drift check skipped'
    }

    It 'does not call gh in report-only mode when rules are unmapped' {
        New-Catalog -Root $script:Root -FileName 'vbr-01_SecurityComplianceCatalog.csv' -Mapped $true, $false, $false
        $out = & $script:DriftScript -CatalogPath $script:Glob @Common -ReportOnly 6>&1 | Out-String
        $out | Should -Match 'Report-only: 2 unmapped rule'
        $global:VhcGhCalls | Should -HaveCount 0
    }

    It 'does not call gh in report-only mode when all rules are mapped' {
        New-Catalog -Root $script:Root -FileName 'vbr-01_SecurityComplianceCatalog.csv' -Mapped $true, $true
        & $script:DriftScript -CatalogPath $script:Glob @Common -ReportOnly 6>&1 | Out-Null
        $global:VhcGhCalls | Should -HaveCount 0
    }

    It 'opens the sca-drift issue when unmapped rules are found and report-only is off' {
        New-Catalog -Root $script:Root -FileName 'vbr-01_SecurityComplianceCatalog.csv' -Mapped $false
        & $script:DriftScript -CatalogPath $script:Glob @Common 6>&1 | Out-Null
        ($global:VhcGhCalls -join "`n") | Should -Match 'issue create --label sca-drift'
    }
}
