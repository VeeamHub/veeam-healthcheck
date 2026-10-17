#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

# Test-VhcLiveRun.Tests.ps1
# Pester v5 tests for .github/scripts/Test-VhcLiveRun.ps1 (the shared assertions of the live-VBR CI
# jobs). Every check is run end to end in a child pwsh so the script's `exit 1` is observed as a real
# process exit code, against small fixture HTML/JSON/PDF/PPTX/log files in $TestDrive.
# Picked up automatically by pester-tests.yml (it runs every tracked *.Tests.ps1).

BeforeAll {
    $script:LiveScript = Join-Path $PSScriptRoot 'Test-VhcLiveRun.ps1'
    $script:Pwsh = (Get-Process -Id $PID).Path
    $script:Counter = 0

    function New-FixtureDir {
        $script:Counter++
        $d = Join-Path $TestDrive ("case{0}" -f $script:Counter)
        New-Item -ItemType Directory -Path $d -Force | Out-Null
        return $d
    }

    # Runs one check in a child process; returns the exit code and the combined output.
    function Invoke-Live {
        param([string[]]$ArgList)
        $out = & $script:Pwsh -NoProfile -File $script:LiveScript @ArgList 2>&1 | Out-String
        [pscustomobject]@{ Exit = $LASTEXITCODE; Out = $out }
    }

    $script:TableOk = '<table><thead><tr><th>A</th></tr></thead><tbody><tr><td>1</td></tr></tbody></table>'
    $script:TableHeaderOnly = '<table><thead><tr><th>A</th></tr></thead><tbody></tbody></table>'
    $script:TablePlaceholder = '<table><thead><tr><th>A</th></tr></thead><tbody><tr><td colspan="12"><em>No proxy data available.</em></td></tr></tbody></table>'
    $script:Grid = '<div class="security-grid"><div class="security-item"><div class="label">MFA</div></div></div>'

    function New-Card {
        param([string]$Id, [string]$Inner)
        '<div class="section-card open" id="' + $Id + '"><div class="section-header"><h2>' + $Id + '</h2></div><div class="section-body">' + $Inner + '</div></div>'
    }

    # A report in the current generator's shape. -Omit drops sections, -Override replaces a section's
    # inner HTML, -Extra is appended before </body>.
    function New-ReportHtml {
        param([string[]]$Omit = @(), [hashtable]$Override = @{}, [string]$Extra = '')
        $inner = [ordered]@{
            license    = $script:TableOk
            secsummary = $script:Grid
            vbrserver  = $script:TableOk
            proxies    = $script:TableOk
            repos      = $script:TableOk
            jobsummary = $script:TableOk
            jobs       = (New-Card 'jobTable-backup-vmware-backup' $script:TableOk)
        }
        foreach ($k in $Override.Keys) { $inner[$k] = $Override[$k] }
        $nav = '<a href="#license">License</a><a href="#jobTable-backup-vmware-backup">Backup Jobs</a>'
        $body = ''
        foreach ($id in $inner.Keys) {
            if ($Omit -contains $id) { continue }
            $body += New-Card $id $inner[$id]
        }
        # A later card with a table: a section with no table of its own must not borrow this one.
        $body += New-Card 'regkeys' $script:TableOk
        "<html><head><style>.x{width:100%}</style></head><body>$nav$body$Extra</body></html>"
    }

    function Write-Report {
        param([string]$Dir, [string]$Html, [string]$Name = 'report.html')
        $p = Join-Path $Dir $Name
        [IO.File]::WriteAllText($p, $Html, (New-Object Text.UTF8Encoding $false))
        return $p
    }

    function Invoke-ReportCheck {
        param([string]$Check, [string]$Html)
        $d = New-FixtureDir
        $p = Write-Report -Dir $d -Html $Html
        Invoke-Live @('-Check', $Check, '-ReportPath', $p)
    }

    function New-Zip {
        param([string]$Path, [hashtable]$Entries)
        Add-Type -AssemblyName System.IO.Compression
        $fs = [IO.File]::Create($Path)
        $zip = New-Object IO.Compression.ZipArchive ($fs, [IO.Compression.ZipArchiveMode]::Create)
        foreach ($name in $Entries.Keys) {
            $e = $zip.CreateEntry($name)
            $w = New-Object IO.StreamWriter ($e.Open(), (New-Object Text.UTF8Encoding $false))
            $w.Write($Entries[$name])
            $w.Dispose()
        }
        $zip.Dispose()
        $fs.Dispose()
    }

    $script:SlideWithText = '<p:sld xmlns:a="x"><a:p><a:r><a:t>Hello</a:t></a:r></a:p></p:sld>'
    $script:PdfGood = "%PDF-1.4`n1 0 obj << /Type /Pages /Count 1 >> endobj`n2 0 obj << /Type /Page /Parent 1 0 R >> endobj`n%%EOF"

    # Writes report.html + report.pdf + report.pptx into a fresh dir and returns the dir.
    function New-ExportsDir {
        param($Pdf = $script:PdfGood, $Pptx = @{ 'ppt/slides/slide1.xml' = $script:SlideWithText }, [switch]$NoPdf, [switch]$NoPptx)
        $d = New-FixtureDir
        [void](Write-Report -Dir $d -Html (New-ReportHtml))
        if (-not $NoPdf) { [IO.File]::WriteAllBytes((Join-Path $d 'report.pdf'), [Text.Encoding]::ASCII.GetBytes([string]$Pdf)) }
        if (-not $NoPptx) { New-Zip -Path (Join-Path $d 'report.pptx') -Entries $Pptx }
        return $d
    }
}

Describe 'ReportContent' {
    It 'passes a good report (all core sections, hyphenated nav link, optional sections absent)' {
        $r = Invoke-ReportCheck 'ReportContent' (New-ReportHtml)
        $r.Exit | Should -Be 0
    }

    It 'fails when no HTML report exists (explicit path missing)' {
        $r = Invoke-Live @('-Check', 'ReportContent', '-ReportPath', (Join-Path $TestDrive 'nope.html'))
        $r.Exit | Should -Not -Be 0
        $r.Out | Should -Match 'no HTML report found'
    }

    It 'fails when the report directory has no HTML' {
        $d = New-FixtureDir
        $r = Invoke-Live @('-Check', 'ReportContent', '-ReportDir', $d)
        $r.Exit | Should -Not -Be 0
    }

    It 'fails on an empty report file' {
        $r = Invoke-ReportCheck 'ReportContent' ''
        $r.Exit | Should -Not -Be 0
    }

    It 'fails when core section <_> is missing' -ForEach @('license', 'secsummary', 'vbrserver', 'proxies', 'repos', 'jobsummary', 'jobs') {
        $r = Invoke-ReportCheck 'ReportContent' (New-ReportHtml -Omit @($_))
        $r.Exit | Should -Not -Be 0
        $r.Out | Should -Match "id=`"$_`""
    }

    It 'does not accept a section id that only appears in script, style or comments' {
        $decoy = '<script>var s=''<div id="license">'';</script><style>/* id="license" */</style><!-- <div id="license"> -->'
        $r = Invoke-ReportCheck 'ReportContent' (New-ReportHtml -Omit @('license') -Extra $decoy)
        $r.Exit | Should -Not -Be 0
    }

    It 'does not accept the section label as plain text without the element id' {
        $r = Invoke-ReportCheck 'ReportContent' (New-ReportHtml -Omit @('license') -Extra '<p>License Summary</p>')
        $r.Exit | Should -Not -Be 0
    }

    It 'fails on a dead navigation link with a hyphenated id' {
        $r = Invoke-ReportCheck 'ReportContent' (New-ReportHtml -Extra '<a href="#jobTable-no-such-card">x</a>')
        $r.Exit | Should -Not -Be 0
        $r.Out | Should -Match 'jobTable-no-such-card'
    }

    It 'passes a navigation link whose hyphenated id exists' {
        $r = Invoke-ReportCheck 'ReportContent' (New-ReportHtml -Extra '<a href="#jobTable-backup-vmware-backup">ok</a>')
        $r.Exit | Should -Be 0
    }

    It 'fails on a percentage above 100' {
        $r = Invoke-ReportCheck 'ReportContent' (New-ReportHtml -Extra '<p>Usage 150%</p>')
        $r.Exit | Should -Not -Be 0
    }
}

Describe 'ReportTables' {
    It 'passes a good report' {
        $r = Invoke-ReportCheck 'ReportTables' (New-ReportHtml)
        $r.Exit | Should -Be 0
    }

    It 'fails when no HTML report exists' {
        $r = Invoke-Live @('-Check', 'ReportTables', '-ReportPath', (Join-Path $TestDrive 'nope.html'))
        $r.Exit | Should -Not -Be 0
    }

    It 'fails when core section <_> is missing' -ForEach @('license', 'secsummary', 'vbrserver', 'proxies', 'repos', 'jobsummary', 'jobs') {
        $r = Invoke-ReportCheck 'ReportTables' (New-ReportHtml -Omit @($_))
        $r.Exit | Should -Not -Be 0
    }

    It 'fails when the first table of <_> has only a header row' -ForEach @('license', 'vbrserver', 'proxies', 'repos', 'jobsummary') {
        $r = Invoke-ReportCheck 'ReportTables' (New-ReportHtml -Override @{ $_ = $script:TableHeaderOnly })
        $r.Exit | Should -Not -Be 0
        $r.Out | Should -Match 'no data rows'
    }

    It 'fails when the only data row is a "No data available" placeholder' {
        $r = Invoke-ReportCheck 'ReportTables' (New-ReportHtml -Override @{ proxies = $script:TablePlaceholder })
        $r.Exit | Should -Not -Be 0
    }

    It 'fails when a core section has no table and a later card does (no borrowing)' {
        $r = Invoke-ReportCheck 'ReportTables' (New-ReportHtml -Override @{ repos = '<p>nothing here</p>' })
        $r.Exit | Should -Not -Be 0
        $r.Out | Should -Match 'no table'
    }

    It 'fails when the jobs container holds no job table' {
        $r = Invoke-ReportCheck 'ReportTables' (New-ReportHtml -Override @{ jobs = '' })
        $r.Exit | Should -Not -Be 0
    }

    It 'fails when the security summary renders no status tiles' {
        $r = Invoke-ReportCheck 'ReportTables' (New-ReportHtml -Override @{ secsummary = '<div class="security-grid"></div>' })
        $r.Exit | Should -Not -Be 0
    }

    It 'does not fail on empty optional sections (SOBR, extents, tiers, object storage)' {
        $extra = (New-Card 'sobr' $script:TableHeaderOnly) + (New-Card 'extents' $script:TableHeaderOnly) + (New-Card 'objstorage' $script:TablePlaceholder)
        $r = Invoke-ReportCheck 'ReportTables' (New-ReportHtml -Extra $extra)
        $r.Exit | Should -Be 0
    }
}

Describe 'SessionLog' {
    BeforeAll {
        function New-LogDir {
            param([string]$Content)
            $d = New-FixtureDir
            if ($null -ne $Content) { Set-Content -Path (Join-Path $d 'CollectorVeeamConfigScript.log') -Value $Content }
            return $d
        }
    }

    It 'fails when there is no collection log' {
        $r = Invoke-Live @('-Check', 'SessionLog', '-LogDir', (New-LogDir $null))
        $r.Exit | Should -Not -Be 0
    }

    It 'fails when the log never shows the session fetch' {
        $r = Invoke-Live @('-Check', 'SessionLog', '-LogDir', (New-LogDir "unrelated line`nExported 5 session rows to x.csv"))
        $r.Exit | Should -Not -Be 0
    }

    It 'fails when the log has no session-report outcome' {
        $r = Invoke-Live @('-Check', 'SessionLog', '-LogDir', (New-LogDir 'Fetching backup sessions for the last 7 days...'))
        $r.Exit | Should -Not -Be 0
    }

    It 'passes fetch plus exported rows' {
        $r = Invoke-Live @('-Check', 'SessionLog', '-RequireSessionRows', '-LogDir', (New-LogDir "Fetching backup sessions for the last 7 days...`nExported 12 session rows to x.csv"))
        $r.Exit | Should -Be 0
    }

    It 'fails with -RequireSessionRows when zero rows were produced' {
        $r = Invoke-Live @('-Check', 'SessionLog', '-RequireSessionRows', '-LogDir', (New-LogDir "Fetching backup sessions for the last 7 days...`nNo session rows produced - writing header-only CSV"))
        $r.Exit | Should -Not -Be 0
    }

    It 'fails with -RequireSessionRows when the exported count is 0' {
        $r = Invoke-Live @('-Check', 'SessionLog', '-RequireSessionRows', '-LogDir', (New-LogDir "Fetching backup sessions for the last 7 days...`nExported 0 session rows to x.csv"))
        $r.Exit | Should -Not -Be 0
    }

    It 'allows "no session rows" when rows are not required' {
        $r = Invoke-Live @('-Check', 'SessionLog', '-LogDir', (New-LogDir "Fetching backup sessions for the last 7 days...`nNo session rows produced - writing header-only CSV"))
        $r.Exit | Should -Be 0
    }
}

Describe 'Exports' {
    It 'passes a good PDF and PPTX' {
        $d = New-ExportsDir
        (Invoke-Live @('-Check', 'Exports', '-ReportDir', $d)).Exit | Should -Be 0
    }

    It 'fails when the HTML report is missing' {
        $d = New-FixtureDir
        (Invoke-Live @('-Check', 'Exports', '-ReportDir', $d)).Exit | Should -Not -Be 0
    }

    It 'fails when the PDF is missing' {
        $d = New-ExportsDir -NoPdf
        (Invoke-Live @('-Check', 'Exports', '-ReportDir', $d)).Exit | Should -Not -Be 0
    }

    It 'fails when the PPTX is missing' {
        $d = New-ExportsDir -NoPptx
        (Invoke-Live @('-Check', 'Exports', '-ReportDir', $d)).Exit | Should -Not -Be 0
    }

    It 'fails on a 0-byte PDF' {
        $d = New-ExportsDir -Pdf ''
        (Invoke-Live @('-Check', 'Exports', '-ReportDir', $d)).Exit | Should -Not -Be 0
    }

    It 'fails when the PDF does not start with %PDF' {
        $d = New-ExportsDir -Pdf "<html>not a pdf</html> /Type /Page"
        $r = Invoke-Live @('-Check', 'Exports', '-ReportDir', $d)
        $r.Exit | Should -Not -Be 0
        $r.Out | Should -Match '%PDF'
    }

    It 'fails when the PDF has no page objects' {
        $d = New-ExportsDir -Pdf "%PDF-1.4`n1 0 obj << /Type /Catalog >> endobj`n%%EOF"
        $r = Invoke-Live @('-Check', 'Exports', '-ReportDir', $d)
        $r.Exit | Should -Not -Be 0
        $r.Out | Should -Match 'no page objects'
    }

    It 'does not count the /Type /Pages tree node as a page' {
        $d = New-ExportsDir -Pdf "%PDF-1.4`n1 0 obj << /Type /Pages /Count 0 >> endobj`n%%EOF"
        (Invoke-Live @('-Check', 'Exports', '-ReportDir', $d)).Exit | Should -Not -Be 0
    }

    It 'fails when the PPTX is not a zip' {
        $d = New-ExportsDir
        [IO.File]::WriteAllText((Join-Path $d 'report.pptx'), 'this is not a zip archive')
        $r = Invoke-Live @('-Check', 'Exports', '-ReportDir', $d)
        $r.Exit | Should -Not -Be 0
        $r.Out | Should -Match 'not a valid zip'
    }

    It 'fails when the PPTX has no slide entries' {
        $d = New-ExportsDir -Pptx @{ '[Content_Types].xml' = '<Types/>'; 'ppt/presentation.xml' = '<p/>' }
        $r = Invoke-Live @('-Check', 'Exports', '-ReportDir', $d)
        $r.Exit | Should -Not -Be 0
        $r.Out | Should -Match 'no ppt/slides'
    }

    It 'fails when no PPTX slide contains text' {
        $d = New-ExportsDir -Pptx @{ 'ppt/slides/slide1.xml' = '<p:sld xmlns:a="x"><a:p><a:r><a:t>   </a:t></a:r></a:p></p:sld>' }
        $r = Invoke-Live @('-Check', 'Exports', '-ReportDir', $d)
        $r.Exit | Should -Not -Be 0
        $r.Out | Should -Match 'none contain text'
    }

    It 'ignores slide rels files when counting slides' {
        $d = New-ExportsDir -Pptx @{ 'ppt/slides/_rels/slide1.xml.rels' = '<Relationships/>' }
        (Invoke-Live @('-Check', 'Exports', '-ReportDir', $d)).Exit | Should -Not -Be 0
    }
}

Describe 'Scrub' {
    BeforeAll {
        function New-AnonDir {
            param([string]$HtmlExtra = '', [string]$Json = '{"OrphanedSupersededBackups":[]}')
            $d = New-FixtureDir
            $html = '<html><body><div id="orphanedsupersededbackups"><h2>Orphaned &amp; Superseded Backups</h2></div>' + $HtmlExtra + '</body></html>'
            [void](Write-Report -Dir $d -Html $html)
            if ($Json) { Set-Content -Path (Join-Path $d 'report.json') -Value $Json }
            return $d
        }
    }

    It 'passes a clean scrubbed report' {
        $d = New-AnonDir
        (Invoke-Live @('-Check', 'Scrub', '-AnonDir', $d, '-ScrubHost', 'vbr-real-host')).Exit | Should -Be 0
    }

    It 'fails when no hostname is supplied (vacuous leak check)' {
        $d = New-AnonDir
        (Invoke-Live @('-Check', 'Scrub', '-AnonDir', $d)).Exit | Should -Not -Be 0
    }

    It 'fails when the real hostname leaks into the HTML' {
        $d = New-AnonDir -HtmlExtra '<p>vbr-real-host</p>'
        (Invoke-Live @('-Check', 'Scrub', '-AnonDir', $d, '-ScrubHost', 'vbr-real-host')).Exit | Should -Not -Be 0
    }

    It 'fails when the real hostname leaks into the JSON' {
        $d = New-AnonDir -Json '{"OrphanedSupersededBackups":[],"Server":"VBR-REAL-HOST"}'
        (Invoke-Live @('-Check', 'Scrub', '-AnonDir', $d, '-ScrubHost', 'vbr-real-host')).Exit | Should -Not -Be 0
    }

    It 'fails when the Orphaned section is missing' {
        $d = New-FixtureDir
        [void](Write-Report -Dir $d -Html '<html><body><style>/* Orphaned & Superseded Backups */</style></body></html>')
        Set-Content -Path (Join-Path $d 'report.json') -Value '{"OrphanedSupersededBackups":[]}'
        (Invoke-Live @('-Check', 'Scrub', '-AnonDir', $d, '-ScrubHost', 'vbr-real-host')).Exit | Should -Not -Be 0
    }

    It 'fails when the scrubbed JSON export is missing' {
        $d = New-AnonDir -Json ''
        (Invoke-Live @('-Check', 'Scrub', '-AnonDir', $d, '-ScrubHost', 'vbr-real-host')).Exit | Should -Not -Be 0
    }

    It 'checks every supplied hostname (second name leaks)' {
        $d = New-AnonDir -HtmlExtra '<p>second-name</p>'
        . $script:LiveScript
        $f = Test-VhcScrubbedReport -HtmlDir $d -HostName @('vbr-real-host', 'second-name') -NewerThan ([datetime]::MinValue)
        @($f).Count | Should -BeGreaterThan 0
    }
}
