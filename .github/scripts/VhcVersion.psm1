# VhcVersion.psm1
# Commit-driven release version computation for vHC. See docs/adr/0031 and 0032.
#
#   Version = Major.Minor.Patch.Revision
#   Major.Minor.Patch : computed from the Conventional Commits since the Base Tag,
#                       raised to the csproj Major.Minor floor, or set by a
#                       'Release-As: X.Y.Z' commit footer.
#   Revision          : the CI run number, passed in by the caller.
#   Base Tag          : the highest GA tag (vMajor.Minor.Patch.Revision, no suffix)
#                       among ALL fetched tags, NOT `git describe`'s nearest tag and NOT
#                       limited to tags reachable from the ref: GA tags sit on master's
#                       merge commits, which dev never merges back.

Set-StrictMode -Version Latest

$script:GaTagPattern  = '^v(\d+)\.(\d+)\.(\d+)\.(\d+)$'
$script:AnyTagPattern = '^v(\d+)\.(\d+)\.(\d+)\.(\d+)(-dev)?$'
$script:BumpRank      = @{ none = 0; patch = 1; minor = 2; major = 3 }
$script:CommitTypes   = 'feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert'

function ConvertTo-VhcTagInfo {
    [CmdletBinding()]
    param([string]$Name, [string]$Pattern)

    $m = [regex]::Match($Name, $Pattern)
    if (-not $m.Success) { return $null }
    [pscustomobject]@{
        Name    = $Name
        Version = [version]"$($m.Groups[1].Value).$($m.Groups[2].Value).$($m.Groups[3].Value).$($m.Groups[4].Value)"
    }
}

function Select-VhcHighestTag {
    # Highest-versioned tag by numeric order. GA tags only unless -IncludeDev.
    [CmdletBinding()]
    param([string[]]$Tags, [switch]$IncludeDev)

    $pattern = if ($IncludeDev) { $script:AnyTagPattern } else { $script:GaTagPattern }
    $found = @($Tags | ForEach-Object { ConvertTo-VhcTagInfo -Name $_ -Pattern $pattern } |
        Where-Object { $null -ne $_ })
    if ($found.Count -eq 0) { return $null }
    $found | Sort-Object -Property Version -Descending | Select-Object -First 1
}

function Get-VhcCommitBump {
    # Highest bump implied by the commits: none | patch | minor | major.
    [CmdletBinding()]
    param([object[]]$Commits)

    $bump = 'none'
    foreach ($c in $Commits) {
        $subject = "$($c.Subject)"
        $body    = "$($c.Body)"
        if ($subject -match '^[A-Za-z]+(\([^)]*\))?!:' -or $body -cmatch '(?m)^BREAKING[ -]CHANGE:') {
            $level = 'major'
        } elseif ($subject -match '^feat(\([^)]*\))?:') {
            $level = 'minor'
        } else {
            $level = 'patch'
        }
        if ($script:BumpRank[$level] -gt $script:BumpRank[$bump]) { $bump = $level }
    }
    $bump
}

function Get-VhcReleaseAs {
    # Highest 'Release-As: X.Y.Z' footer across the commits, or $null. Throws on a malformed footer.
    [CmdletBinding()]
    param([object[]]$Commits)

    $highest = $null
    foreach ($c in $Commits) {
        foreach ($line in ("$($c.Body)" -split "`r?`n")) {
            if ($line -notmatch '^\s*Release-As\s*:') { continue }
            if ($line -notmatch '^Release-As: (\d+)\.(\d+)\.(\d+)\s*$') {
                throw "Malformed Release-As footer '$($line.Trim())' (expected 'Release-As: X.Y.Z')."
            }
            $v = [version]"$($Matches[1]).$($Matches[2]).$($Matches[3])"
            if ($null -eq $highest -or $v -gt $highest) { $highest = $v }
        }
    }
    $highest
}

function Get-VhcFloor {
    # The csproj Major.Minor as a three-part version Major.Minor.0.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$CsprojPath)

    $csproj = [xml](Get-Content -LiteralPath $CsprojPath -Raw)
    $group  = @($csproj.Project.PropertyGroup)[0]
    $parts  = "$($group.AssemblyVersion)".Split('.')
    if ($parts.Count -lt 2) { throw "No AssemblyVersion with Major.Minor found in $CsprojPath." }
    [version]"$($parts[0]).$($parts[1]).0"
}

function Resolve-VhcVersion {
    # Combine base, bump, floor and Release-As into a three-part version plus the reason.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][version]$Base,
        [Parameter(Mandatory)][ValidateSet('none', 'patch', 'minor', 'major')][string]$Bump,
        [Parameter(Mandatory)][version]$Floor,
        [version]$ReleaseAs
    )

    if ($null -ne $ReleaseAs) {
        if ($ReleaseAs -lt $Floor) { throw "Release-As $ReleaseAs is below the csproj floor $Floor." }
        if ($ReleaseAs -le $Base)  { throw "Release-As $ReleaseAs is not above the last GA version $Base." }
        return [pscustomobject]@{ Version = $ReleaseAs; Reason = 'release-as' }
    }

    $derived = switch ($Bump) {
        'major' { [version]::new($Base.Major + 1, 0, 0) }
        'minor' { [version]::new($Base.Major, $Base.Minor + 1, 0) }
        'patch' { [version]::new($Base.Major, $Base.Minor, $Base.Build + 1) }
        'none'  { $Base }
    }
    if ($Floor -gt $derived) { return [pscustomobject]@{ Version = $Floor; Reason = 'floor' } }
    [pscustomobject]@{ Version = $derived; Reason = $Bump }
}

function Assert-VhcVersionOverride {
    # Validate a manual four-part override; it must be above the last GA version when one exists.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Override, [version]$BaseTagVersion)

    if ($Override -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "Version override '$Override' must be four numeric parts (Major.Minor.Patch.Revision)."
    }
    $v = [version]$Override
    if ($null -ne $BaseTagVersion -and $v -le $BaseTagVersion) {
        throw "Version override $Override is not above the last GA version $BaseTagVersion."
    }
    $v
}

function Invoke-VhcGit {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$RepoPath, [Parameter(Mandatory)][string[]]$Arguments)

    # stderr goes to a temp file, not `2>&1`: a git warning on an exit-0 call must not
    # end up in the parsed stdout. It is only reported when git fails.
    $errFile = [System.IO.Path]::GetTempFileName()
    try {
        $out     = & git -C $RepoPath @Arguments 2>$errFile
        $exit    = $LASTEXITCODE
        $errText = (Get-Content -LiteralPath $errFile -Raw)
    } finally {
        Remove-Item -LiteralPath $errFile -Force -ErrorAction SilentlyContinue
    }
    if ($exit -ne 0) { throw "git $($Arguments -join ' ') failed: $(@($out) -join ' ') $errText".TrimEnd() }
    $out
}

function Get-VhcCommitsSince {
    # Non-merge commits reachable from $Ref and not from $From (whole history when $From is empty).
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$RepoPath, [Parameter(Mandatory)][string]$Ref, [string]$From)

    $range = if ($From) { "$From..$Ref" } else { $Ref }
    $raw = (Invoke-VhcGit -RepoPath $RepoPath -Arguments @('log', $range, '--no-merges', '--pretty=format:%H%x1f%s%x1f%b%x1e', '--')) -join "`n"
    $records = $raw -split [char]0x1E
    foreach ($record in $records) {
        if ([string]::IsNullOrWhiteSpace($record)) { continue }
        $f = $record -split [char]0x1F, 3
        [pscustomobject]@{
            Sha     = $f[0].Trim()
            Subject = $f[1].Trim()
            Body    = $(if ($f.Count -gt 2) { $f[2] } else { '' })
        }
    }
}

function Get-VhcVersionInfo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$Revision,
        [string]$Ref = 'HEAD',
        [string]$RepoPath = '.',
        [string]$CsprojPath,
        [ValidateSet('ga', 'dev')][string]$Channel = 'ga',
        [string]$VersionOverride
    )

    if (-not $CsprojPath) { $CsprojPath = Join-Path $RepoPath 'vHC/HC_Reporting/VeeamHealthCheck.csproj' }

    # All fetched tags, not `tag --merged $Ref`: dev cannot reach GA tags on master's merge commits.
    $tags     = @(Invoke-VhcGit -RepoPath $RepoPath -Arguments @('tag', '--list'))
    $baseTag  = Select-VhcHighestTag -Tags $tags
    $baseName = if ($baseTag) { $baseTag.Name } else { '' }
    $base3    = if ($baseTag) { [version]::new($baseTag.Version.Major, $baseTag.Version.Minor, $baseTag.Version.Build) } else { [version]'0.0.0' }

    $commits = @(Get-VhcCommitsSince -RepoPath $RepoPath -Ref $Ref -From $baseName)
    $floor   = Get-VhcFloor -CsprojPath $CsprojPath

    if ($VersionOverride) {
        $baseFull = if ($baseTag) { $baseTag.Version } else { $null }
        $version  = (Assert-VhcVersionOverride -Override $VersionOverride -BaseTagVersion $baseFull).ToString()
        $reason   = 'override'
        $bump     = 'n/a'
    } else {
        $bump      = Get-VhcCommitBump -Commits $commits
        $releaseAs = Get-VhcReleaseAs -Commits $commits
        $resolved  = Resolve-VhcVersion -Base $base3 -Bump $bump -Floor $floor -ReleaseAs $releaseAs
        $v         = $resolved.Version
        $version   = "$($v.Major).$($v.Minor).$($v.Build).$Revision"
        $reason    = $resolved.Reason
    }

    $notesFrom = $baseName
    if ($Channel -eq 'dev') {
        $anyTag = Select-VhcHighestTag -Tags $tags -IncludeDev
        $notesFrom = if ($anyTag) { $anyTag.Name } else { '' }
    }

    [pscustomobject]@{
        Version      = $version
        Reason       = $reason
        Bump         = $bump
        BaseTag      = $baseName
        NotesFromTag = $notesFrom
        Floor        = $floor.ToString()
        CommitCount  = $commits.Count
        Channel      = $Channel
    }
}

function Test-VhcCommitMessage {
    # Conventional Commits problems for one message; an empty result means it is fine.
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Subject, [string]$Body = '')

    $problems = [System.Collections.Generic.List[string]]::new()
    $isRevert = $Subject -match '^Revert "'
    if (-not $isRevert -and $Subject -notmatch "^($script:CommitTypes)(\([^)]+\))?!?: \S") {
        $problems.Add("Subject '$Subject' is not '<type>(<scope>)!: <description>' (types: $($script:CommitTypes -replace '\|', ', ')).")
    }
    foreach ($line in ("$Body" -split "`r?`n")) {
        if ($line -match '^\s*Release-As\s*:' -and $line -notmatch '^Release-As: \d+\.\d+\.\d+\s*$') {
            $problems.Add("Malformed footer '$($line.Trim())' (expected 'Release-As: X.Y.Z').")
        }
    }
    $problems.ToArray()
}

Export-ModuleMember -Function Select-VhcHighestTag, Get-VhcCommitBump, Get-VhcReleaseAs, Get-VhcFloor, Resolve-VhcVersion, Assert-VhcVersionOverride, Invoke-VhcGit, Get-VhcCommitsSince, Get-VhcVersionInfo, Test-VhcCommitMessage
