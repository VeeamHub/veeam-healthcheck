#Requires -Version 7.0
# Pester v5 tests for Invoke-VhciCBackupSessionFetch

BeforeAll {
    . $PSCommandPath.Replace('.Tests.ps1', '.ps1')
}

Describe 'Invoke-VhciCBackupSessionFetch' {

    # Check the Mandatory attribute directly instead of calling the function with a
    # parameter missing: a missing mandatory parameter makes PowerShell prompt for it
    # unless the host is -NonInteractive, which can hang a test run.
    It 'declares <Name> as a mandatory parameter' -ForEach @(
        @{ Name = 'JobId' }
        @{ Name = 'Since' }
        @{ Name = 'Until' }
    ) {
        $param = (Get-Command Invoke-VhciCBackupSessionFetch).Parameters[$Name]
        $param | Should -Not -BeNullOrEmpty
        ($param.Attributes | Where-Object { $_ -is [System.Management.Automation.ParameterAttribute] }).Mandatory | Should -Contain $true
    }

    It 'accepts [guid] JobId, [datetime] Since and [datetime] Until without a ParameterBindingException' {
        # In a non-Veeam environment the static .NET call inside the wrapper
        # throws because [Veeam.Backup.Core.CBackupSession] is not loaded.
        # That's expected and tolerated. What we DO want to catch is a
        # ParameterBindingException, which would mean the parameter names or
        # types stopped matching what the caller passes - a regression in the
        # wrapper's contract. The actual argument forwarding to the static
        # method is verified indirectly by Get-VhciJobSessions' GJS-3 tests via
        # Pester's Mock - which is the right place for that assertion since
        # the wrapper is intentionally a Mock seam (see ADR 0018).
        $err = $null
        try { Invoke-VhciCBackupSessionFetch -JobId ([guid]::NewGuid()) -Since (Get-Date).AddDays(-7) -Until (Get-Date) }
        catch { $err = $_ }
        if ($null -ne $err) {
            $err.Exception | Should -Not -BeOfType [System.Management.Automation.ParameterBindingException]
        }
    }
}
