#Requires -Version 7.0
# Pester v5 unit tests for the E2E check functions. Mock-based (synthetic files in
# a temp dir) - no build, no live VBR, no tool run. UNTAGGED so CI runs them.
# The full live/import harness (Invoke-VhcE2E.ps1) is exercised manually / on-VBR.

BeforeAll {
    . (Join-Path $PSScriptRoot 'VhcE2EChecks.ps1')
    $script:tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("vhce2e-" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
}
AfterAll { Remove-Item $script:tmp -Recurse -Force -ErrorAction SilentlyContinue }

Describe 'Test-VhcHtmlReport' {
    It 'fails when the report is missing' {
        (Test-VhcHtmlReport -Path (Join-Path $tmp 'nope.html')).Pass | Should -BeFalse
    }
    It 'fails when the report is too small' {
        $p = Join-Path $tmp 'small.html'; Set-Content $p '<html>tiny</html>'
        (Test-VhcHtmlReport -Path $p -MinBytes 20000).Pass | Should -BeFalse
    }
    It 'fails when a required anchor is missing' {
        $p = Join-Path $tmp 'noanchor.html'; Set-Content $p ('x' * 30000 + '<div id="jobs">')
        (Test-VhcHtmlReport -Path $p -MinBytes 1000 -RequiredAnchors 'id="jobs"','id="license"').Pass | Should -BeFalse
    }
    It 'fails when an error marker leaked into the report' {
        $p = Join-Path $tmp 'leak.html'; Set-Content $p ('x' * 30000 + 'Unhandled exception')
        (Test-VhcHtmlReport -Path $p -MinBytes 1000).Pass | Should -BeFalse
    }
    It 'passes a well-formed report with all anchors and no leaks' {
        $p = Join-Path $tmp 'good.html'; Set-Content $p ('x' * 30000 + '<div id="jobs"></div><div id="license"></div>')
        (Test-VhcHtmlReport -Path $p -MinBytes 1000 -RequiredAnchors 'id="jobs"','id="license"').Pass | Should -BeTrue
    }
}

Describe 'Test-VhcCsvSet' {
    BeforeAll {
        $script:csvDir = Join-Path $tmp 'csvs'; New-Item -ItemType Directory -Path $csvDir -Force | Out-Null
        1..6 | ForEach-Object { "a,b`n1,2" | Set-Content (Join-Path $csvDir "host_file$_.csv") }
        'Name,JobType' + "`nJ1,Backup" | Set-Content (Join-Path $csvDir 'host_Jobs.csv')
        'Id,Name' + "`n1,R1" | Set-Content (Join-Path $csvDir 'host_Repositories.csv')
        'Id,Name' + "`n1,S1" | Set-Content (Join-Path $csvDir 'host_Servers.csv')
    }
    It 'passes when required CSVs present and all parse' {
        (Test-VhcCsvSet -Dir $csvDir -RequiredCsvs '_Jobs.csv','_Repositories.csv','_Servers.csv' -MinFiles 3).Pass | Should -BeTrue
    }
    It 'fails when a required CSV is missing' {
        (Test-VhcCsvSet -Dir $csvDir -RequiredCsvs '_DoesNotExist.csv' -MinFiles 3).Pass | Should -BeFalse
    }
    It 'fails when the dir is missing' {
        (Test-VhcCsvSet -Dir (Join-Path $tmp 'nodir')).Pass | Should -BeFalse
    }
}

Describe 'Test-VhcLogFile' {
    It 'passes a clean completed log' {
        $p = Join-Path $tmp 'clean.log'; "INFO start`nINFO Starting RUN...complete!" | Set-Content $p
        (Test-VhcLogFile -Path $p).Pass | Should -BeTrue
    }
    It 'fails when completion marker missing' {
        $p = Join-Path $tmp 'incomplete.log'; "INFO start" | Set-Content $p
        (Test-VhcLogFile -Path $p).Pass | Should -BeFalse
    }
    It 'fails on ERROR lines by default' {
        $p = Join-Path $tmp 'err.log'; "ERROR boom`nINFO Starting RUN...complete!" | Set-Content $p
        (Test-VhcLogFile -Path $p).Pass | Should -BeFalse
    }
    It 'surfaces but allows ERRORs with -AllowErrors' {
        $p = Join-Path $tmp 'err2.log'; "ERROR boom`nINFO Starting RUN...complete!" | Set-Content $p
        $r = Test-VhcLogFile -Path $p -AllowErrors
        $r.Pass | Should -BeTrue; $r.Data | Should -Be 1
    }
}

Describe 'Test-VhcJsonReport / Compare-VhcJsonBaseline' {
    BeforeAll {
        $script:rep = Join-Path $tmp 'rep.json'
        @{ Sections = @{ jobInfo = @{ Headers=@('Name'); Rows=@(@('J1'),@('J2')) }; repos = @{ Headers=@('Name'); Rows=@(@('R1')) } } } |
            ConvertTo-Json -Depth 8 | Set-Content $script:rep
        $script:same = Join-Path $tmp 'same.json'; Copy-Item $script:rep $script:same
        $script:diff = Join-Path $tmp 'diff.json'
        @{ Sections = @{ jobInfo = @{ Headers=@('Name'); Rows=@(@('J1')) }; repos = @{ Headers=@('Name'); Rows=@(@('R1')) } } } |
            ConvertTo-Json -Depth 8 | Set-Content $script:diff
    }
    It 'passes when required sections meet min rows' {
        (Test-VhcJsonReport -Path $rep -RequiredSections @{ jobInfo = 1; repos = 1 }).Pass | Should -BeTrue
    }
    It 'fails when a section is under its min row count' {
        (Test-VhcJsonReport -Path $rep -RequiredSections @{ jobInfo = 5 }).Pass | Should -BeFalse
    }
    It 'baseline diff passes for identical reports' {
        (Compare-VhcJsonBaseline -CurrentPath $same -BaselinePath $rep).Pass | Should -BeTrue
    }
    It 'baseline diff fails and names the drifted section' {
        $r = Compare-VhcJsonBaseline -CurrentPath $diff -BaselinePath $rep
        $r.Pass | Should -BeFalse; $r.Data | Should -Contain 'jobInfo'
    }
}
