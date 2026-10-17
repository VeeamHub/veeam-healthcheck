#Requires -Version 7.0
# Pester v5 unit tests for the validation-harness guards.
# These are MOCK-BASED and do NOT touch a live VBR, so they are intentionally
# UNTAGGED - the default CI Invoke-Pester run executes them. The live integration
# tests (Integration/*.Tests.ps1) carry -Tag 'LiveVBR' and are excluded in CI.
#
# Focus: the safety-critical reconcile logic and the prefix contract - the parts
# that, if wrong, could let a harness run touch a user's objects.

BeforeAll {
    . (Join-Path $PSScriptRoot 'VhcvGuards.ps1')

    # Helper to build a snapshot-shaped object from simple Id/Name pairs.
    function script:NewSnap {
        param([hashtable] $Coll)
        $o = [ordered]@{}
        foreach ($c in 'Jobs', 'TapeJobs', 'NasJobs', 'SureBackup', 'Labs', 'AppGroups', 'Repos') {
            $o[$c] = @(if ($Coll.ContainsKey($c)) { $Coll[$c] } else { @() })
        }
        [pscustomobject]$o
    }
    function script:Obj { param($Id, $Name) [pscustomobject]@{ Id = $Id; Name = $Name } }
}

Describe 'Guard A: New-VhcvName' {
    It 'always carries the hard vHC-VALIDATE- prefix' {
        New-VhcvName -Type 'ViBackup' | Should -BeLike 'vHC-VALIDATE-ViBackup-*'
    }
    It 'encodes the variant' {
        New-VhcvName -Type 'ViBackup' -Variant 'enc-on' | Should -BeLike 'vHC-VALIDATE-ViBackup-enc-on-*'
    }
    It 'produces unique names on repeated calls' {
        $a = New-VhcvName -Type 'X'; $b = New-VhcvName -Type 'X'
        $a | Should -Not -Be $b
    }
}

Describe 'Guard D: Assert-VhcvReconciled' {
    It 'passes when current == baseline and no residue' {
        $base = NewSnap @{ Jobs = @(Obj 1 'UserJobA'), (Obj 2 'UserJobB'); Repos = @(Obj 9 'UserRepo') }
        $now  = NewSnap @{ Jobs = @(Obj 1 'UserJobA'), (Obj 2 'UserJobB'); Repos = @(Obj 9 'UserRepo') }
        Assert-VhcvReconciled -Baseline $base -Current $now | Should -BeTrue
    }

    It 'FAILS (leak) when a vHC-VALIDATE- object survives cleanup' {
        $base = NewSnap @{ Jobs = @(Obj 1 'UserJobA') }
        $now  = NewSnap @{ Jobs = @((Obj 1 'UserJobA'), (Obj 7 'vHC-VALIDATE-ViBackup-base-abc123')) }
        { Assert-VhcvReconciled -Baseline $base -Current $now } | Should -Throw '*leaked Jobs*'
    }

    It 'FAILS (missing) when a baseline object disappeared' {
        $base = NewSnap @{ Jobs = @((Obj 1 'UserJobA'), (Obj 2 'UserJobB')) }
        $now  = NewSnap @{ Jobs = @(Obj 1 'UserJobA') }
        { Assert-VhcvReconciled -Baseline $base -Current $now } | Should -Throw '*baseline Jobs missing*'
    }

    It 'checks every reconciled collection, not just Jobs' {
        $base = NewSnap @{ Repos = @(Obj 9 'UserRepo') }
        $now  = NewSnap @{ Repos = @((Obj 9 'UserRepo'), (Obj 10 'vHC-VALIDATE-repo-base-def456')) }
        { Assert-VhcvReconciled -Baseline $base -Current $now } | Should -Throw '*leaked Repos*'
    }

    It 'throws when no baseline is available' {
        { Assert-VhcvReconciled -Baseline $null -Current (NewSnap @{}) } | Should -Throw '*no baseline*'
    }
}
