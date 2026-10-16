#Requires -Version 7.0
# Pester tests for the SCA merge/patch logic (ScaTriage.psm1) and the single proposer
# (Invoke-ScaDriftProposal.ps1). gh and git are recording stand-ins; fixtures are synthetic.

BeforeAll {
    $script:Proposer = $PSCommandPath.Replace('.Tests.ps1', '.ps1').Replace('Invoke-ScaDriftProposal.ps1', 'Invoke-ScaDriftProposal.ps1')
    Import-Module (Join-Path $PSScriptRoot 'ScaTriage.psm1') -Force

    # Recording stand-ins. $global:VhcGh / $global:VhcGit are scriptblocks taking the joined args and
    # returning output; they may set $global:LASTEXITCODE to simulate failure.
    function global:gh {
        $line = $args -join ' '
        $global:VhcCalls.Add("gh $line")
        $global:LASTEXITCODE = 0
        & $global:VhcGh $line
    }
    function global:git {
        $line = $args -join ' '
        $global:VhcCalls.Add("git $line")
        $global:LASTEXITCODE = 0
        & $global:VhcGit $line
    }

    function New-Triage {
        param([string]$Server, [string]$Version, [object[]]$Rules, [string[]]$Dormant = @(), [bool]$Rest = $true)
        $rows = foreach ($r in $Rules) {
            [pscustomobject]@{ enum = $r.enum; id = ''; status = 'Passed'; labelSource = 'JsonMap'; consoleLabel = $r.console
                mapLabel = $r.map; verdict = $r.verdict; proposedLabel = $r.proposed }
        }
        [pscustomobject]@{
            schema = 'sca-triage/1'; server = $Server; vbrMajor = '13'; vbrVersion = $Version; validatedFor = '13.0.0'
            rest = [pscustomobject]@{ attempted = $true; available = $Rest; apiVersion = '1.2-rev0'; reason = $(if ($Rest) { 'ok' } else { 'REST unavailable: test' }) }
            verified = $Rest; counts = [pscustomobject]@{}; evaluated = @($rows)
            dormant = @($Dormant | ForEach-Object { [pscustomobject]@{ enum = $_; mapped = $false; mapLabel = $null } })
        }
    }
    function Save-Triage {
        param($Dir, $Triage)
        $d = Join-Path $Dir "sca-triage-$($Triage.server)"
        New-Item -ItemType Directory -Path $d -Force | Out-Null
        $Triage | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $d 'sca-triage.json')
    }
}

AfterAll {
    Remove-Item function:gh, function:git -ErrorAction SilentlyContinue
    Remove-Variable -Name VhcCalls, VhcGh, VhcGit -Scope Global -ErrorAction SilentlyContinue
}

Describe 'Merge-ScaTriage' {
    It 'dedups the same drifting rule seen on two servers and uses the highest version' {
        $a = New-Triage -Server 'vbr-2' -Version '13.1.1.18' -Rules @(@{ enum = 'RuleC'; console = 'Brand new'; map = $null; verdict = 'missing'; proposed = 'Brand new' })
        $b = New-Triage -Server 'vbr-13-sql' -Version '13.0.1.1071' -Rules @(@{ enum = 'RuleC'; console = 'Brand new'; map = $null; verdict = 'missing'; proposed = 'Brand new' })
        $m = Merge-ScaTriage -Triage @($a, $b)
        $m.drift | Should -HaveCount 1
        $m.drift[0].servers | Should -Be @('vbr-13-sql', 'vbr-2')
        $m.patchable | Should -HaveCount 1
        $m.highestVersion | Should -Be '13.1.1.18'
    }

    It 'keeps dormant enums out of drift and drops ones another server evaluated' {
        $a = New-Triage -Server 's1' -Version '13.1.1.18' -Rules @(@{ enum = 'R1'; console = 'L'; map = 'L'; verdict = 'ok'; proposed = $null }) -Dormant 'R2', 'R3'
        $b = New-Triage -Server 's2' -Version '13.1.1.18' -Rules @(@{ enum = 'R2'; console = 'L2'; map = 'L2'; verdict = 'ok'; proposed = $null }) -Dormant 'R3'
        $m = Merge-ScaTriage -Triage @($a, $b)
        $m.drift | Should -HaveCount 0
        @($m.dormant.enum) | Should -Be @('R3')
    }

    It 'reports a conflict instead of guessing when servers disagree on a console label' {
        $a = New-Triage -Server 's1' -Version '13.1.1.18' -Rules @(@{ enum = 'R1'; console = 'New label'; map = 'Old'; verdict = 'label-changed'; proposed = 'New label' })
        $b = New-Triage -Server 's2' -Version '12.3.0.1' -Rules @(@{ enum = 'R1'; console = 'Old'; map = 'Old'; verdict = 'ok'; proposed = $null })
        $m = Merge-ScaTriage -Triage @($a, $b)
        $m.conflicts | Should -HaveCount 1
        $m.patchable | Should -HaveCount 0
        $m.needsLabel | Should -HaveCount 0
        $m.drift[0].conflict | Should -Match 'New label'
    }

    It 'lists a CSV-only missing rule as needing a console label' {
        $a = New-Triage -Server 's1' -Version $null -Rest $false -Rules @(@{ enum = 'R9'; console = $null; map = $null; verdict = 'missing'; proposed = $null })
        $m = Merge-ScaTriage -Triage @($a)
        $m.needsLabel | Should -HaveCount 1
        $m.patchable | Should -HaveCount 0
        $m.restUnavailable | Should -HaveCount 1
    }
}

Describe 'Update-VbrConfigLabels' {
    BeforeAll {
        $script:Cfg = "{`n  `"A`": 1,`n  `"SecurityComplianceRulesValidatedForVbrVersion`": `"13.0.0`",`n  `"SecurityComplianceRuleNames`": {`n    `"RuleA`": `"Label A`",`n    `"RuleB`": `"Label B`"`n  },`n  `"Z`": 2`n}`n"
    }

    It 'replaces a changed label, appends a new one, bumps the version and leaves the rest alone' {
        $new = Update-VbrConfigLabels -Text $script:Cfg -Labels ([ordered]@{ RuleB = 'Label B v2'; RuleC = 'Rule "C" caf' + [char]0xE9 }) -ValidatedFor '13.1.1.18'
        $p = $new | ConvertFrom-Json
        $p.SecurityComplianceRuleNames.RuleA | Should -BeExactly 'Label A'
        $p.SecurityComplianceRuleNames.RuleB | Should -BeExactly 'Label B v2'
        $p.SecurityComplianceRuleNames.RuleC | Should -BeExactly ('Rule "C" caf' + [char]0xE9)
        $p.SecurityComplianceRulesValidatedForVbrVersion | Should -Be '13.1.1.18'
        $p.Z | Should -Be 2
        $new | Should -Match '^[\x00-\x7F]*$'
        ($new -split "`n").Count | Should -Be (($script:Cfg -split "`n").Count + 1)
    }

    It 'preserves CRLF line endings' {
        $crlf = $script:Cfg -replace "`n", "`r`n"
        $new = Update-VbrConfigLabels -Text $crlf -Labels ([ordered]@{ RuleC = 'Label C' })
        ($new -split "`r`n").Count | Should -Be (($crlf -split "`r`n").Count + 1)
        ($new -replace "`r`n", '').Contains("`n") | Should -BeFalse
    }

    It 'is a no-op when the labels already match' {
        (Update-VbrConfigLabels -Text $script:Cfg -Labels ([ordered]@{ RuleA = 'Label A' })) | Should -BeExactly $script:Cfg
    }
}

Describe 'Invoke-ScaDriftProposal' {
    BeforeEach {
        $global:VhcCalls = [System.Collections.Generic.List[string]]::new()
        $global:VhcGh = { param($l) if ($l -like 'issue list*' -or $l -like 'pr list*') { return '[]' }; if ($l -like 'pr create*') { return 'https://example.invalid/pr/7' }; if ($l -like 'issue create*') { return 'https://example.invalid/issues/9' } }
        $global:VhcGit = { param($l) if ($l -like 'fetch origin bot/*' -or $l -like 'diff --quiet*') { $global:LASTEXITCODE = 1 } }
        $script:Root = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid())
        $script:In = Join-Path $script:Root 'in'
        $script:Out = Join-Path $script:Root 'out'
        New-Item -ItemType Directory -Path $script:In -Force | Out-Null
        $script:Cfg = Join-Path $script:Root 'VbrConfig.json'
        "{`n  `"SecurityComplianceRulesValidatedForVbrVersion`": `"13.0.0`",`n  `"SecurityComplianceRuleNames`": {`n    `"RuleA`": `"Label A`"`n  }`n}`n" | Set-Content -Path $script:Cfg -NoNewline
        $script:Common = @{ TriageDir = $script:In; Repo = 'org/repo'; RunUrl = 'https://example.invalid/run/1'; ConfigPath = $script:Cfg; OutDir = $script:Out }
        $script:Drift = New-Triage -Server 'vbr-2' -Version '13.1.1.18' -Rules @(
            @{ enum = 'RuleA'; console = 'Label A'; map = 'Label A'; verdict = 'ok'; proposed = $null },
            @{ enum = 'RuleC'; console = 'Brand new'; map = $null; verdict = 'missing'; proposed = 'Brand new' })
        $script:Clean = New-Triage -Server 'vbr-2' -Version '13.1.1.18' -Rules @(@{ enum = 'RuleA'; console = 'Label A'; map = 'Label A'; verdict = 'ok'; proposed = $null }) -Dormant 'Never1'
    }
    AfterEach { Remove-Item -Path $script:Root -Recurse -Force -ErrorAction SilentlyContinue }

    It 'report-only makes zero gh or git calls and still writes the proposed patch' {
        Save-Triage -Dir $script:In -Triage $script:Drift
        & $script:Proposer @Common -ReportOnly 6>&1 | Out-Null
        $global:VhcCalls | Should -HaveCount 0
        $proposed = Get-Content (Join-Path $script:Out 'VbrConfig.proposed.json') -Raw | ConvertFrom-Json
        $proposed.SecurityComplianceRuleNames.RuleC | Should -Be 'Brand new'
        $proposed.SecurityComplianceRulesValidatedForVbrVersion | Should -Be '13.1.1.18'
        Get-Content $script:Cfg -Raw | Should -Not -Match 'Brand new'
    }

    It 'report-only makes zero calls on a clean run too' {
        Save-Triage -Dir $script:In -Triage $script:Clean
        & $script:Proposer @Common -ReportOnly 6>&1 | Out-Null
        $global:VhcCalls | Should -HaveCount 0
    }

    It 'opens ONE draft AI-assisted PR from the bot branch and an issue linking it' {
        Save-Triage -Dir $script:In -Triage $script:Drift
        Save-Triage -Dir $script:In -Triage (New-Triage -Server 'vbr-13-sql' -Version '13.1.1.18' -Rules @(
                @{ enum = 'RuleC'; console = 'Brand new'; map = $null; verdict = 'missing'; proposed = 'Brand new' }))
        & $script:Proposer @Common 6>&1 | Out-Null
        $calls = $global:VhcCalls -join "`n"
        @($global:VhcCalls | Where-Object { $_ -like 'gh pr create*' }) | Should -HaveCount 1
        $calls | Should -Match 'gh pr create --draft --base dev --head bot/sca-labels'
        $calls | Should -Match '--label AI-assisted'
        $calls | Should -Match 'git push --force-with-lease origin bot/sca-labels'
        @($global:VhcCalls | Where-Object { $_ -like 'gh issue create*' }) | Should -HaveCount 1
        (Get-Content (Join-Path $script:Out 'issue-body.md') -Raw) | Should -Match 'https://example.invalid/pr/7'
        (Get-Content $script:Cfg -Raw) | Should -Match '"RuleC": "Brand new"'
    }

    It 'does not merge anything' {
        Save-Triage -Dir $script:In -Triage $script:Drift
        & $script:Proposer @Common 6>&1 | Out-Null
        ($global:VhcCalls -join "`n") | Should -Not -Match 'merge'
    }

    It 'updates the existing PR and issue instead of creating duplicates' {
        Save-Triage -Dir $script:In -Triage $script:Drift
        $global:VhcGh = { param($l) if ($l -like 'issue list*') { return '[{"number":4}]' }; if ($l -like 'pr list*') { return '[{"number":7,"url":"https://example.invalid/pr/7"}]' } }
        & $script:Proposer @Common 6>&1 | Out-Null
        $calls = $global:VhcCalls -join "`n"
        $calls | Should -Not -Match 'gh pr create'
        $calls | Should -Not -Match 'gh issue create'
        $calls | Should -Match 'gh pr edit 7'
        $calls | Should -Match 'gh issue edit 4'
    }

    It 'puts the exact changes in the issue when the PR cannot be created' {
        Save-Triage -Dir $script:In -Triage $script:Drift
        $global:VhcGh = { param($l) if ($l -like 'issue list*' -or $l -like 'pr list*') { return '[]' }; if ($l -like 'pr create*') { $global:LASTEXITCODE = 1; return 'GitHub Actions is not permitted to create or approve pull requests' } }
        & $script:Proposer @Common 6>&1 3>&1 | Out-Null
        $body = Get-Content (Join-Path $script:Out 'issue-body.md') -Raw
        $body | Should -Match 'could not create the PR'
        $body | Should -Match 'RuleC: Brand new'
        @($global:VhcCalls | Where-Object { $_ -like 'gh issue create*' }) | Should -HaveCount 1
    }

    It 'closes the PR and the issue on a clean run' {
        Save-Triage -Dir $script:In -Triage $script:Clean
        $global:VhcGh = { param($l) if ($l -like 'issue list*') { return '[{"number":4}]' }; if ($l -like 'pr list*') { return '[{"number":7,"url":"u"}]' } }
        & $script:Proposer @Common 6>&1 | Out-Null
        $calls = $global:VhcCalls -join "`n"
        $calls | Should -Match 'gh pr close 7'
        $calls | Should -Match 'gh issue close 4'
        $calls | Should -Not -Match 'git '
    }

    It 'touches nothing when no triage artifacts exist (no data is not clean)' {
        & $script:Proposer @Common 6>&1 3>&1 | Out-Null
        $global:VhcCalls | Should -HaveCount 0
    }

    It 'a dormant-only fleet never opens an issue' {
        Save-Triage -Dir $script:In -Triage $script:Clean
        & $script:Proposer @Common 6>&1 | Out-Null
        ($global:VhcCalls -join "`n") | Should -Not -Match 'issue create|pr create'
    }
}
