#Requires -Version 7.0
# Pester v5 tests for the script side of the "no password on the command line" channel.
#
# vHC (PsCredentialChannel.cs) puts the password in the child's VHC_CRED_PASSWORD environment
# variable. Each credentialed collector script carries the same small function that reads the
# variable once and removes it from the process environment. These tests extract that function from
# the real script files (via the PowerShell AST, so the scripts themselves are not run) and check it.

# Pester 5 runs a file in two phases: discovery (BeforeDiscovery, Context/Describe bodies and
# -ForEach data) and run (BeforeAll and the It bodies). Variables set in BeforeDiscovery do not
# exist in the run phase, so the script list is built in BOTH places: BeforeDiscovery feeds the
# -ForEach below, BeforeAll feeds the It blocks that read $script:ScriptFiles directly.
BeforeDiscovery {
    $hcReporting = Resolve-Path (Join-Path $PSScriptRoot '../../../..')
    $script:ScriptFiles = @(
        (Join-Path $hcReporting 'Tools/Scripts/HealthCheck/VBR/Get-VBRConfig.ps1'),
        (Join-Path $hcReporting 'Tools/Scripts/HealthCheck/VB365/Collect-VB365Data.ps1'),
        (Join-Path $PSScriptRoot 'TestMfa.ps1')
    ) | ForEach-Object { @{ Path = $_; Name = Split-Path $_ -Leaf } }
}

BeforeAll {
    $hcReporting = Resolve-Path (Join-Path $PSScriptRoot '../../../..')
    $script:ScriptFiles = @(
        (Join-Path $hcReporting 'Tools/Scripts/HealthCheck/VBR/Get-VBRConfig.ps1'),
        (Join-Path $hcReporting 'Tools/Scripts/HealthCheck/VB365/Collect-VB365Data.ps1'),
        (Join-Path $PSScriptRoot 'TestMfa.ps1')
    ) | ForEach-Object { @{ Path = $_; Name = Split-Path $_ -Leaf } }

    function Get-ScriptAst([string]$Path) {
        $tokens = $null; $errors = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors)
        if ($errors.Count -gt 0) { throw "Parse errors in ${Path}: $($errors -join '; ')" }
        $ast
    }

    function Get-ReaderFunctionText([string]$Path) {
        $ast = Get-ScriptAst $Path
        $fn = $ast.Find({
            param($n)
            $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
            $n.Name -eq 'Get-VhcCredentialPasswordFromEnvironment'
        }, $true)
        if ($null -eq $fn) { return $null }
        $fn.Extent.Text
    }
}

Describe 'Get-VhcCredentialPasswordFromEnvironment' {

    AfterEach {
        [Environment]::SetEnvironmentVariable('VHC_CRED_PASSWORD', $null, 'Process')
    }

    Context '<Name>' -ForEach $script:ScriptFiles {

        It 'defines the reader function' {
            Get-ReaderFunctionText $Path | Should -Not -BeNullOrEmpty
        }

        It 'returns the password from the environment and removes it from the process environment' {
            . ([scriptblock]::Create((Get-ReaderFunctionText $Path)))
            [Environment]::SetEnvironmentVariable('VHC_CRED_PASSWORD', "Zq!7'p`"w\`$``Xk9", 'Process')

            $value = Get-VhcCredentialPasswordFromEnvironment

            $value | Should -Be "Zq!7'p`"w\`$``Xk9"
            [Environment]::GetEnvironmentVariable('VHC_CRED_PASSWORD', 'Process') | Should -BeNullOrEmpty
            $env:VHC_CRED_PASSWORD | Should -BeNullOrEmpty
        }

        It 'returns null when the variable is not set' {
            . ([scriptblock]::Create((Get-ReaderFunctionText $Path)))
            [Environment]::SetEnvironmentVariable('VHC_CRED_PASSWORD', $null, 'Process')

            Get-VhcCredentialPasswordFromEnvironment | Should -BeNullOrEmpty
        }

        It 'does not pass the password on to a process started afterwards' {
            . ([scriptblock]::Create((Get-ReaderFunctionText $Path)))
            [Environment]::SetEnvironmentVariable('VHC_CRED_PASSWORD', 'inherit-me', 'Process')
            $null = Get-VhcCredentialPasswordFromEnvironment

            $pwsh = (Get-Process -Id $PID).Path
            $seenByChild = & $pwsh -NoProfile -Command '[Environment]::GetEnvironmentVariable(''VHC_CRED_PASSWORD'', ''Process'')'

            $seenByChild | Should -BeNullOrEmpty
        }

        It 'reads the variable before the script uses any password parameter' {
            $text = Get-Content -Raw $Path
            $callAt = $text.IndexOf('= Get-VhcCredentialPasswordFromEnvironment')
            $callAt | Should -BeGreaterThan 0
        }

        It 'does not log or print the password variable' {
            $text = Get-Content -Raw $Path
            $text | Should -Not -Match 'Write-(Host|Output|Verbose|Debug)[^\r\n]*\$env:VHC_CRED_PASSWORD'
        }
    }

    It 'is the identical function in every script (no drift between copies)' {
        $script:ScriptFiles.Count | Should -Be 3
        $texts = @($script:ScriptFiles | ForEach-Object { Get-ReaderFunctionText $_.Path })
        $texts | ForEach-Object { $_ | Should -Not -BeNullOrEmpty }
        ($texts | Select-Object -Unique).Count | Should -Be 1
    }
}

Describe 'manual use keeps working' {

    It 'Get-VBRConfig.ps1 still declares -Password and -PasswordBase64' {
        $path = ($script:ScriptFiles | Where-Object { $_.Name -eq 'Get-VBRConfig.ps1' }).Path
        $ast = Get-ScriptAst $path
        $names = $ast.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath }
        $names | Should -Contain 'Password'
        $names | Should -Contain 'PasswordBase64'
    }

    It 'Collect-VB365Data.ps1 and TestMfa.ps1 still declare -PasswordBase64' {
        foreach ($name in 'Collect-VB365Data.ps1', 'TestMfa.ps1') {
            $path = ($script:ScriptFiles | Where-Object { $_.Name -eq $name }).Path
            $names = (Get-ScriptAst $path).ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath }
            $names | Should -Contain 'PasswordBase64'
        }
    }
}
