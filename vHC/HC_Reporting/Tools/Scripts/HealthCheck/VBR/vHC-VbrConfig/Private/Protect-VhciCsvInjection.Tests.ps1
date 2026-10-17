#Requires -Version 7.0
# Pester v5 tests for Protect-VhciCsvInjection / ConvertTo-VhciCsvSafeValue.
# Pure PS functions, no live VBR. Proves CSV/spreadsheet formula injection is
# neutralized (leading = + - @ TAB CR on non-numeric strings) while legitimate
# numbers and safe values are preserved, and object shape/column order is kept.

BeforeAll {
    . $PSCommandPath.Replace('.Tests.ps1', '.ps1')
}

Describe 'ConvertTo-VhciCsvSafeValue' {
    Context 'neutralizes formula-shaped non-numeric strings' {
        It 'prefixes a quote for leading "<lead>"' -ForEach @(
            @{ lead = '=' ; in = '=cmd|''/c calc''!A1' }
            @{ lead = '+' ; in = '+cmd()' }
            @{ lead = '@' ; in = '@SUM(A1)' }
            @{ lead = '-' ; in = '-2+3+cmd()' }
        ) {
            ConvertTo-VhciCsvSafeValue -Value $in | Should -Be ("'" + $in)
        }

        It 'neutralizes a leading TAB' {
            $v = ([char]9 + 'evil')
            ConvertTo-VhciCsvSafeValue -Value $v | Should -Be ("'" + $v)
        }

        It 'neutralizes a leading CR' {
            $v = ([char]13 + 'evil')
            ConvertTo-VhciCsvSafeValue -Value $v | Should -Be ("'" + $v)
        }
    }

    Context 'preserves legitimate values' {
        It 'leaves number "<in>" unchanged (no corruption of report data)' -ForEach @(
            @{ in = '-5' }
            @{ in = '+3.2' }
            @{ in = '-1.5' }
            @{ in = '-1000' }
            @{ in = '-2.5e3' }
        ) {
            ConvertTo-VhciCsvSafeValue -Value $in | Should -Be $in
        }

        It 'leaves safe string "<in>" unchanged' -ForEach @(
            @{ in = 'PROD - Aux Infra' }
            @{ in = 'server01.home.lab' }
            @{ in = 'DOMAIN\svc' }
            @{ in = 'VMware Backup' }
        ) {
            ConvertTo-VhciCsvSafeValue -Value $in | Should -Be $in
        }

        It 'passes empty string and null through' {
            ConvertTo-VhciCsvSafeValue -Value '' | Should -Be ''
            ConvertTo-VhciCsvSafeValue -Value $null | Should -Be $null
        }

        It 'passes non-string values (int) through unchanged' {
            ConvertTo-VhciCsvSafeValue -Value 42 | Should -Be 42
        }
    }
}

Describe 'Protect-VhciCsvInjection' {
    It 'neutralizes a malicious property while preserving safe and numeric ones' {
        $obj = [pscustomobject]@{ JobName = '=cmd()'; Repo = 'Repo01'; ChangeRate = '-5' }
        $out = $obj | Protect-VhciCsvInjection
        $out.JobName    | Should -Be "'=cmd()"
        $out.Repo       | Should -Be 'Repo01'
        $out.ChangeRate | Should -Be '-5'
    }

    It 'preserves property order so Export-Csv columns are stable' {
        $obj = [pscustomobject]@{ A = '=x'; B = 'b'; C = '+y' }
        $out = $obj | Protect-VhciCsvInjection
        ($out.PSObject.Properties.Name) | Should -Be @('A', 'B', 'C')
    }

    It 'passes non-string property values through unchanged' {
        $obj = [pscustomobject]@{ Count = 3; Name = '@evil' }
        $out = $obj | Protect-VhciCsvInjection
        $out.Count | Should -Be 3
        $out.Name  | Should -Be "'@evil"
    }

    It 'sanitizes every object in a pipeline of many' {
        $objs = @(
            [pscustomobject]@{ N = '=a' }
            [pscustomobject]@{ N = 'safe' }
            [pscustomobject]@{ N = '-7' }
        )
        $out = $objs | Protect-VhciCsvInjection
        $out[0].N | Should -Be "'=a"
        $out[1].N | Should -Be 'safe'
        $out[2].N | Should -Be '-7'
    }
}
