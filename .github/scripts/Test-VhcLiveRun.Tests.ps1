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

Describe 'CsvOutputs' {
    BeforeAll {
        $script:RepoRootDir = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
        $script:Gold = Join-Path $script:RepoRootDir 'vHC\HC_Reporting\Tools\GoldenBaselines'

        # The golden baselines are the real header source, so the good set is the golden CSVs under a
        # hostname prefix. Golden has no sidecar/meta files; those get minimal valid one-row stand-ins.
        function New-CsvOutputsRoot {
            $root = New-FixtureDir
            $d = Join-Path $root 'host\20260101'
            New-Item -ItemType Directory -Path $d -Force | Out-Null
            foreach ($f in (Get-ChildItem (Join-Path $script:Gold 'VBRConfig') -File -Filter '*.csv')) {
                $n = if ($f.Name.StartsWith('_')) { "localhost$($f.Name)" } else { "localhost_$($f.Name)" }
                Copy-Item $f.FullName (Join-Path $d $n)
            }
            Copy-Item (Join-Path $script:Gold 'SessionReports\VeeamSessionReport.csv') $d
            "`"SweepRan`"`r`n`"True`"" | Set-Content (Join-Path $d 'localhost_orphanedSupersededBackupsMeta.csv')
            "`"Name`",`"Value`"`r`n`"a`",`"b`"" | Set-Content (Join-Path $d 'localhost_SecurityComplianceMeta.csv')
            "`"Server`",`"Type`"`r`n`"s`",`"t`"" | Set-Content (Join-Path $d 'localhost_AllServersRequirementsComparison.csv')
            "`"Name`",`"Success`"`r`n`"VbrInfo`",`"True`"" | Set-Content (Join-Path $d 'localhost_CollectionManifest.csv')
            return [pscustomobject]@{ Root = $root; Dir = $d }
        }
        function Invoke-CsvOutputs {
            param($Fx, [string[]]$Extra = @())
            Invoke-Live (@('-Check', 'CsvOutputs', '-CsvRoot', $Fx.Root) + $Extra)
        }
    }

    It 'passes on the golden baselines (real headers, real collector enumeration)' {
        $fx = New-CsvOutputsRoot
        $r = Invoke-CsvOutputs $fx
        $r.Out | Should -Match 'CsvOutputs passed'
        $r.Exit | Should -Be 0
    }

    It 'fails a missing required CSV and names the file and its collector' {
        $fx = New-CsvOutputsRoot
        Remove-Item (Join-Path $fx.Dir 'localhost_Servers.csv')
        $r = Invoke-CsvOutputs $fx
        $r.Exit | Should -Be 1
        $r.Out | Should -Match 'Servers\.csv.*Get-VhcServer\.ps1'
    }

    It 'fails a required CSV whose header lost a schema column' {
        $fx = New-CsvOutputsRoot
        $p = Join-Path $fx.Dir 'localhost_Jobs.csv'
        $lines = Get-Content $p
        $lines[0] = $lines[0].Replace('"JobType",', '')
        Set-Content $p $lines
        $r = Invoke-CsvOutputs $fx
        $r.Exit | Should -Be 1
        $r.Out | Should -Match 'Jobs\.csv header broken.*JobType'
        $r.Out | Should -Match 'Get-VhcJob\.ps1'
    }

    It 'fails an optional CSV with a malformed header (duplicate column)' {
        $fx = New-CsvOutputsRoot
        "`"Name`",`"Name`"`r`n`"a`",`"b`"" | Set-Content (Join-Path $fx.Dir 'localhost_TapeLibraries.csv')
        $r = Invoke-CsvOutputs $fx
        $r.Exit | Should -Be 1
        $r.Out | Should -Match 'TapeLibraries\.csv.*duplicate'
    }

    It 'fails a required CSV that has a header and no rows' {
        $fx = New-CsvOutputsRoot
        $p = Join-Path $fx.Dir 'localhost_Repositories.csv'
        Set-Content $p (Get-Content $p -TotalCount 1)
        $r = Invoke-CsvOutputs $fx
        $r.Exit | Should -Be 1
        $r.Out | Should -Match 'Repositories\.csv.*0 data rows'
    }

    It 'allows an optional feature CSV to be absent' {
        $fx = New-CsvOutputsRoot
        Remove-Item (Join-Path $fx.Dir 'localhost_TapeJobs.csv')
        (Invoke-CsvOutputs $fx).Exit | Should -Be 0
    }

    It 'enforces per-server expectations: vbr-12 needs regkeys, and passes when it is there' {
        $fx = New-CsvOutputsRoot
        # labCommon CSVs that golden does not carry
        foreach ($n in 'Credentials', 'EmailNotification', 'Gateways', 'ObjectStorageRepos', 'RepositoryServers', 'TapeMediaPools') {
            "`"Name`",`"Id`"`r`n`"a`",`"b`"" | Set-Content (Join-Path $fx.Dir "localhost_$n.csv")
        }
        $reg = Join-Path $fx.Dir 'localhost_regkeys.csv'
        $bak = Join-Path $TestDrive 'regkeys.bak'
        Move-Item $reg $bak
        $r = Invoke-CsvOutputs $fx @('-LabProfile', 'vbr-12')
        $r.Exit | Should -Be 1
        $r.Out | Should -Match 'regkeys\.csv.*Get-VhcRegistrySettings\.ps1'
        Move-Item $bak $reg
        (Invoke-CsvOutputs $fx @('-LabProfile', 'vbr-12')).Exit | Should -Be 0
    }

    It 'fails on an unknown profile and on a manifest entry no collector writes' {
        $fx = New-CsvOutputsRoot
        (Invoke-CsvOutputs $fx @('-LabProfile', 'nope')).Exit | Should -Be 1
        $m = Join-Path $TestDrive 'm.json'
        '{"required":{"NoSuchCsv":"typo"},"profiles":{}}' | Set-Content $m
        $r = Invoke-CsvOutputs $fx @('-ManifestPath', $m)
        $r.Exit | Should -Be 1
        $r.Out | Should -Match 'NoSuchCsv'
    }

    It 'requires the columns the report reads, not the golden columns the collector never writes' {
        $fx = New-CsvOutputsRoot
        # PhysProtected: the report reads Type; the golden used to list HostName/OSType that no collector writes
        $p = Join-Path $fx.Dir 'localhost_PhysProtected.csv'
        $lines = Get-Content $p
        $lines[0] = $lines[0].Replace('"Type",', '')
        Set-Content $p $lines
        $r = Invoke-CsvOutputs $fx
        $r.Exit | Should -Be 1
        $r.Out | Should -Match 'PhysProtected\.csv header broken.*Type'
        # a column only the old golden listed is not required
        "`"Id`",`"Name`",`"Type`"`r`n`"a`",`"b`",`"Computer`"" | Set-Content $p
        (Invoke-CsvOutputs $fx).Exit | Should -Be 0
    }

    It 'logs a known gap instead of failing when the column is absent' {
        $fx = New-CsvOutputsRoot
        $r = Invoke-CsvOutputs $fx
        $r.Exit | Should -Be 0
        $r.Out | Should -Match "UserRoles\.csv.*no 'Description' column \(known gap"
    }

    It 'accepts a zero-byte NAS CSV but still fails a zero-byte CSV that is not allowed empty' {
        $fx = New-CsvOutputsRoot
        New-Item -ItemType File -Path (Join-Path $fx.Dir 'localhost_NasSharesize.csv') | Out-Null
        $r = Invoke-CsvOutputs $fx
        $r.Exit | Should -Be 0
        $r.Out | Should -Match 'NasSharesize\.csv.*zero-byte'
        New-Item -ItemType File -Path (Join-Path $fx.Dir 'localhost_TapeLibraries.csv') | Out-Null
        $r = Invoke-CsvOutputs $fx
        $r.Exit | Should -Be 1
        $r.Out | Should -Match 'TapeLibraries\.csv.*no header line'
    }

    It 'fails a manifest columns or emptyFileOk entry no collector writes' {
        $fx = New-CsvOutputsRoot
        $m = Join-Path $TestDrive 'm2.json'
        '{"required":{},"profiles":{},"columns":{"NoSuchCsv":{"required":["A"]}},"emptyFileOk":{"AlsoNoSuch":"x"}}' | Set-Content $m
        $r = Invoke-CsvOutputs $fx @('-ManifestPath', $m)
        $r.Exit | Should -Be 1
        $r.Out | Should -Match 'NoSuchCsv'
        $r.Out | Should -Match 'AlsoNoSuch'
    }

    It 'every manifest columns/emptyFileOk entry has a collector and a reader citation' {
        . $script:LiveScript
        $map = Get-VhcCollectorCsvMap $script:RepoRootDir
        $exp = Get-Content (Join-Path $PSScriptRoot 'live-csv-expectations.json') -Raw | ConvertFrom-Json
        foreach ($p in $exp.columns.PSObject.Properties) {
            $map.ContainsKey($p.Name) | Should -BeTrue -Because "columns entry '$($p.Name)' must be written by a collector"
            [string]$p.Value.reader | Should -Not -BeNullOrEmpty -Because "columns entry '$($p.Name)' must cite its C# reader"
        }
        foreach ($n in $exp.emptyFileOk.PSObject.Properties.Name) { $map.ContainsKey($n) | Should -BeTrue }
    }

    It 'fails when the CSV directory is missing' {
        $r = Invoke-Live @('-Check', 'CsvOutputs', '-CsvRoot', (New-FixtureDir))
        $r.Exit | Should -Be 1
    }

    It 'has a collector for every manifest entry and enumerates the module writers' {
        . $script:LiveScript
        $map = Get-VhcCollectorCsvMap $script:RepoRootDir
        $map.Count | Should -BeGreaterThan 60
        $map['Servers'].Collector | Should -Match 'Get-VhcServer\.ps1$'
        $map['VeeamSessionReport'].Collector | Should -Match 'Get-VhcSessionReport\.ps1$'
        $map['CollectionManifest'].Collector | Should -Be 'Get-VBRConfig.ps1'
        $exp = Get-Content (Join-Path $PSScriptRoot 'live-csv-expectations.json') -Raw | ConvertFrom-Json
        $names = @($exp.required.PSObject.Properties.Name) + @($exp.labCommon.PSObject.Properties.Name)
        foreach ($p in $exp.profiles.PSObject.Properties) { $names += @($p.Value.nonEmpty.PSObject.Properties.Name) }
        foreach ($n in $names) { $map.ContainsKey($n) | Should -BeTrue -Because "manifest entry '$n' must be written by a collector" }
    }
}
