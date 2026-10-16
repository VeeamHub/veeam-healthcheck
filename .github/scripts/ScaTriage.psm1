#Requires -Version 7
# ScaTriage.psm1
# Deterministic Security & Compliance Analyzer (SCA) label triage. No AI involved:
# every verdict is a join between data the VBR server produced.
#
#   evaluated rules   _SecurityCompliance.csv (RuleType enum name + RuleId guid), the rules the
#                     server actually evaluated and that customers see in the report
#   console labels    REST GET /api/v1/securityAnalyzer/bestPractices (id guid + full console label)
#   label map         VbrConfig.json SecurityComplianceRuleNames (enum name -> label)
#   dormant enums     catalog rows (all VBRBestPracticeType values) that were never evaluated;
#                     reported for information only, never a reason to open an issue
#
# Verdicts for an evaluated rule: ok | missing | label-changed.
# Used by Invoke-SecurityComplianceDriftCheck.ps1 (per runner) and
# Invoke-ScaDriftProposal.ps1 (single proposer). Keep this file ASCII-only.

Set-StrictMode -Version Latest

function Get-ScaProp {
    param($Object, [string]$Name)
    $p = $Object.PSObject.Properties[$Name]
    if ($null -eq $p) { return $null }
    return $p.Value
}

function ConvertTo-ScaGuidKey {
    param($Value)
    $s = "$Value".Trim().Trim('{', '}').ToLowerInvariant()
    return $s
}

function Get-ScaMapLabel {
    param($LabelMap, [string]$Enum)
    if ($null -eq $LabelMap) { return $null }
    $p = $LabelMap.PSObject.Properties[$Enum]
    if ($null -eq $p) { return $null }
    $v = [string]$p.Value
    if ([string]::IsNullOrWhiteSpace($v)) { return $null }
    return $v
}

function ConvertTo-ScaVersion {
    param($Value)
    $parsed = $null
    if ([version]::TryParse("$Value", [ref]$parsed)) { return $parsed }
    return $null
}

function Get-ScaTriage {
    <#
    .Synopsis
        Joins evaluated rules with console labels and the label map; returns the triage object
        that is serialized to sca-triage.json.
    .Parameter Rest
        Result of Get-ScaRestBestPractices, or $null when REST was not attempted.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Server,
        [object[]]$Evaluated = @(),
        [object[]]$Catalog = @(),
        $LabelMap,
        [string]$ValidatedFor = '',
        [string]$VbrMajor = '',
        [string]$VbrVersion = '',
        $Rest
    )

    $consoleById = @{}
    $restAvailable = $false
    if ($null -ne $Rest -and $Rest.Available) {
        $restAvailable = $true
        foreach ($item in @($Rest.Items)) {
            if ($null -ne $item.id) { $consoleById[(ConvertTo-ScaGuidKey $item.id)] = [string]$item.bestPractice }
        }
    }

    $rows = [System.Collections.Generic.List[object]]::new()
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $verifiedAny = $false

    foreach ($e in @($Evaluated)) {
        $enum = [string](Get-ScaProp $e 'RuleType')
        if ([string]::IsNullOrWhiteSpace($enum)) { continue }
        [void]$seen.Add($enum)

        $id = ConvertTo-ScaGuidKey (Get-ScaProp $e 'RuleId')
        $mapLabel = Get-ScaMapLabel -LabelMap $LabelMap -Enum $enum
        $isMapped = "$(Get-ScaProp $e 'IsMapped')" -eq 'True'
        $source = [string](Get-ScaProp $e 'LabelSource')
        $console = $null
        if ($id -and $consoleById.ContainsKey($id)) { $console = $consoleById[$id]; $verifiedAny = $true }

        $verdict = 'ok'
        $proposed = $null
        if ($source -eq 'SdkProperty') {
            $verdict = 'ok'
        }
        elseif ($null -ne $console) {
            if ($null -eq $mapLabel) { $verdict = 'missing'; $proposed = $console }
            elseif ($mapLabel -ine $console) { $verdict = 'label-changed'; $proposed = $console }
        }
        elseif (-not $isMapped -or $null -eq $mapLabel) {
            $verdict = 'missing'
        }

        $rows.Add([pscustomobject][ordered]@{
            enum          = $enum
            id            = $id
            status        = [string](Get-ScaProp $e 'Status')
            labelSource   = $source
            consoleLabel  = $console
            mapLabel      = $mapLabel
            verdict       = $verdict
            proposedLabel = $proposed
        })
    }

    $dormant = foreach ($c in @($Catalog)) {
        $enum = [string]$c.RuleType
        if ([string]::IsNullOrWhiteSpace($enum) -or $seen.Contains($enum)) { continue }
        [pscustomobject][ordered]@{
            enum     = $enum
            mapped   = ("$($c.IsMapped)" -eq 'True')
            mapLabel = Get-ScaMapLabel -LabelMap $LabelMap -Enum $enum
        }
    }
    $dormant = @($dormant)

    $restInfo = [pscustomobject][ordered]@{
        attempted  = ($null -ne $Rest -and [bool]$Rest.Attempted)
        available  = $restAvailable
        apiVersion = $(if ($null -ne $Rest) { $Rest.ApiVersion } else { $null })
        reason     = $(if ($null -ne $Rest) { $Rest.Reason } else { 'REST not attempted' })
    }

    $ok      = @($rows | Where-Object verdict -eq 'ok').Count
    $missing = @($rows | Where-Object verdict -eq 'missing').Count
    $changed = @($rows | Where-Object verdict -eq 'label-changed').Count

    return [pscustomobject][ordered]@{
        schema       = 'sca-triage/1'
        server       = $Server
        vbrMajor     = $VbrMajor
        vbrVersion   = $(if ($VbrVersion) { $VbrVersion } else { $null })
        validatedFor = $ValidatedFor
        rest         = $restInfo
        verified     = $verifiedAny
        counts       = [pscustomobject][ordered]@{
            evaluated      = $rows.Count
            ok             = $ok
            missing        = $missing
            labelChanged   = $changed
            dormant        = $dormant.Count
            dormantUnmapped = @($dormant | Where-Object { -not $_.mapped }).Count
        }
        evaluated    = @($rows)
        dormant      = $dormant
    }
}

function Get-ScaRestBestPractices {
    <#
    .Synopsis
        Fetches console labels from the VBR REST API. Never throws: failure is reported in
        .Available / .Reason so callers degrade to the CSV-only verdict.
        Credentials travel only in the OAuth2 password-grant body and the Bearer header; error
        text is reduced to an HTTP status code or exception type so nothing secret is echoed.
    #>
    [CmdletBinding()]
    param(
        [string]$RestHost,
        [int]$Port = 9419,
        [string]$User,
        [string]$Password,
        [string[]]$ApiVersions = @('1.2-rev0', '1.1-rev2', '1.1-rev1', '1.1-rev0'),
        [int]$TimeoutSec = 30
    )

    $result = [pscustomobject]@{
        Attempted = $false; Available = $false; Reason = ''; ApiVersion = $null; Items = @(); BuildVersion = $null
    }
    if ([string]::IsNullOrWhiteSpace($RestHost) -or [string]::IsNullOrWhiteSpace($User) -or [string]::IsNullOrWhiteSpace($Password)) {
        $result.Reason = 'REST host or credentials not provided'
        return $result
    }
    $result.Attempted = $true
    $base = "https://${RestHost}:${Port}"
    $reasons = [System.Collections.Generic.List[string]]::new()

    foreach ($ver in $ApiVersions) {
        $hdr = @{ 'x-api-version' = $ver }
        try {
            $tok = Invoke-RestMethod -SkipCertificateCheck -TimeoutSec $TimeoutSec -ErrorAction Stop `
                -Method POST -Uri "$base/api/oauth2/token" -Headers $hdr `
                -ContentType 'application/x-www-form-urlencoded' `
                -Body @{ grant_type = 'password'; username = $User; password = $Password }
        }
        catch {
            $code = Get-ScaHttpStatus $_
            $reasons.Add("token $ver : $(if ($code) { "HTTP $code" } else { $_.Exception.GetType().Name })")
            # A rejected credential must not be retried against other API versions (lockout risk).
            if ($code -in 401, 403) { break }
            # Nothing listening at all: other API versions will not help either.
            if (-not $code) { break }
            continue
        }

        $auth = @{ 'x-api-version' = $ver; 'Authorization' = "Bearer $($tok.access_token)" }
        try {
            $info = Invoke-RestMethod -SkipCertificateCheck -TimeoutSec $TimeoutSec -ErrorAction Stop `
                -Uri "$base/api/v1/serverInfo" -Headers $auth
            if ($info -and $info.PSObject.Properties['buildVersion']) { $result.BuildVersion = [string]$info.buildVersion }
        }
        catch { }

        try {
            $resp = Invoke-RestMethod -SkipCertificateCheck -TimeoutSec $TimeoutSec -ErrorAction Stop `
                -Uri "$base/api/v1/securityAnalyzer/bestPractices" -Headers $auth
        }
        catch {
            $code = Get-ScaHttpStatus $_
            $reasons.Add("bestPractices $ver : $(if ($code) { "HTTP $code" } else { $_.Exception.GetType().Name })")
            continue
        }

        $items = @()
        if ($resp.PSObject.Properties['items']) { $items = @($resp.items) }
        elseif ($resp.PSObject.Properties['data']) { $items = @($resp.data) }
        if ($items.Count -eq 0) { $reasons.Add("bestPractices $ver : empty"); continue }

        $result.Available = $true
        $result.ApiVersion = $ver
        $result.Items = $items
        $result.Reason = 'ok'
        return $result
    }

    $result.Reason = 'REST unavailable: ' + ($reasons -join '; ')
    return $result
}

function Get-ScaHttpStatus {
    param($ErrorRecord)
    try {
        $resp = Get-ScaProp $ErrorRecord.Exception 'Response'
        if ($null -ne $resp -and $null -ne $resp.StatusCode) { return [int]$resp.StatusCode }
        $direct = Get-ScaProp $ErrorRecord.Exception 'StatusCode'
        if ($null -ne $direct) { return [int]$direct }
    }
    catch { }
    return $null
}

function Merge-ScaTriage {
    <#
    .Synopsis
        Unions per-server triage objects: one entry per enum, conflicts surfaced instead of guessed.
    #>
    [CmdletBinding()]
    param([object[]]$Triage = @())

    $Triage = @($Triage | Where-Object { $null -ne $_ })
    $byEnum = @{}
    $evaluatedEnums = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $dormantCandidates = @{}
    $versions = [System.Collections.Generic.List[string]]::new()

    foreach ($t in $Triage) {
        if ($t.vbrVersion) { $versions.Add([string]$t.vbrVersion) }
        foreach ($r in @($t.evaluated)) {
            [void]$evaluatedEnums.Add($r.enum)
            if (-not $byEnum.ContainsKey($r.enum)) { $byEnum[$r.enum] = [System.Collections.Generic.List[object]]::new() }
            $byEnum[$r.enum].Add([pscustomobject]@{ server = $t.server; version = $t.vbrVersion; row = $r })
        }
        foreach ($d in @($t.dormant)) {
            if (-not $dormantCandidates.ContainsKey($d.enum)) { $dormantCandidates[$d.enum] = $d }
        }
    }

    $drift = [System.Collections.Generic.List[object]]::new()
    $conflicts = [System.Collections.Generic.List[object]]::new()

    foreach ($enum in ($byEnum.Keys | Sort-Object)) {
        $entries = $byEnum[$enum]
        $bad = @($entries | Where-Object { $_.row.verdict -ne 'ok' })
        if ($bad.Count -eq 0) { continue }

        $consoleLabels = @($entries | Where-Object { $_.row.consoleLabel } |
            ForEach-Object { $_.row.consoleLabel } | Sort-Object -Unique -CaseSensitive:$false)
        $verdict = if (@($bad | Where-Object { $_.row.verdict -eq 'label-changed' }).Count -gt 0) { 'label-changed' } else { 'missing' }
        $servers = @($bad | ForEach-Object { $_.server } | Sort-Object -Unique)
        $current = @($entries | Where-Object { $_.row.mapLabel } | ForEach-Object { $_.row.mapLabel } | Select-Object -First 1)

        $entry = [pscustomobject][ordered]@{
            enum          = $enum
            verdict       = $verdict
            currentLabel  = $(if ($current.Count) { $current[0] } else { $null })
            proposedLabel = $null
            servers       = $servers
        }

        if ($consoleLabels.Count -gt 1) {
            $entry | Add-Member -NotePropertyName conflict -NotePropertyValue ($consoleLabels -join ' | ')
            $conflicts.Add($entry)
        }
        elseif ($consoleLabels.Count -eq 1) {
            $entry.proposedLabel = $consoleLabels[0]
        }
        $drift.Add($entry)
    }

    $highest = $null
    foreach ($v in $versions) {
        $pv = ConvertTo-ScaVersion $v
        if ($null -eq $pv) { continue }
        if ($null -eq $highest -or $pv -gt (ConvertTo-ScaVersion $highest)) { $highest = $v }
    }

    $dormant = @($dormantCandidates.Keys | Where-Object { -not $evaluatedEnums.Contains($_) } | Sort-Object |
        ForEach-Object { $dormantCandidates[$_] })

    return [pscustomobject][ordered]@{
        servers        = @($Triage | ForEach-Object { $_.server })
        highestVersion = $highest
        validatedFor   = $(if ($Triage.Count) { @($Triage)[0].validatedFor } else { '' })
        restUnavailable = @($Triage | Where-Object { -not $_.rest.available } | ForEach-Object { [pscustomobject]@{ server = $_.server; reason = $_.rest.reason } })
        drift          = @($drift)
        patchable      = @($drift | Where-Object { $_.proposedLabel })
        needsLabel     = @($drift | Where-Object { -not $_.proposedLabel -and -not $_.PSObject.Properties['conflict'] })
        conflicts      = @($conflicts)
        dormant        = $dormant
        hasData        = ($Triage.Count -gt 0)
    }
}

function ConvertTo-ScaJsonString {
    <# JSON string literal, ASCII-only (non-ASCII becomes \uXXXX). #>
    param([string]$Value)
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.Append('"')
    foreach ($ch in $Value.ToCharArray()) {
        switch ($ch) {
            '"'  { [void]$sb.Append('\"') }
            '\'  { [void]$sb.Append('\\') }
            default {
                $code = [int]$ch
                if ($code -lt 0x20 -or $code -gt 0x7e) { [void]$sb.Append(('\u{0:x4}' -f $code)) }
                else { [void]$sb.Append($ch) }
            }
        }
    }
    [void]$sb.Append('"')
    return $sb.ToString()
}

function Update-VbrConfigLabels {
    <#
    .Synopsis
        Text-level edit of VbrConfig.json: replaces or appends SecurityComplianceRuleNames entries
        and optionally bumps SecurityComplianceRulesValidatedForVbrVersion. The rest of the file is
        left byte-for-byte alone (no JSON round trip). Throws if the result is not valid JSON or
        does not contain exactly the requested labels.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][System.Collections.IDictionary]$Labels,
        [string]$ValidatedFor
    )

    $eol = if ($Text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $blockRx = [regex]'(?s)("SecurityComplianceRuleNames"\s*:\s*\{)(.*?)(\r?\n[ \t]*\})'
    $m = $blockRx.Match($Text)
    if (-not $m.Success) { throw 'SecurityComplianceRuleNames block not found in VbrConfig.json' }

    $body = $m.Groups[2].Value
    $indent = '    '
    $im = [regex]::Match($body, '\n([ \t]+)"')
    if ($im.Success) { $indent = $im.Groups[1].Value }

    $appends = [System.Collections.Generic.List[string]]::new()
    foreach ($key in $Labels.Keys) {
        $lit = ConvertTo-ScaJsonString ([string]$Labels[$key])
        $entryRx = [regex]('("' + [regex]::Escape($key) + '"\s*:\s*)"(?:[^"\\]|\\.)*"')
        if ($entryRx.IsMatch($body)) {
            $body = $entryRx.Replace($body, [System.Text.RegularExpressions.MatchEvaluator]{ param($x) $x.Groups[1].Value + $lit }, 1)
        }
        else {
            $appends.Add($indent + (ConvertTo-ScaJsonString $key) + ': ' + $lit)
        }
    }
    if ($appends.Count -gt 0) {
        $body = $body.TrimEnd() + ',' + $eol + ($appends -join (',' + $eol))
    }

    $new = $Text.Substring(0, $m.Groups[2].Index) + $body + $Text.Substring($m.Groups[3].Index)

    if ($ValidatedFor) {
        $verRx = [regex]'("SecurityComplianceRulesValidatedForVbrVersion"\s*:\s*)"[^"]*"'
        if (-not $verRx.IsMatch($new)) { throw 'SecurityComplianceRulesValidatedForVbrVersion not found in VbrConfig.json' }
        $vlit = ConvertTo-ScaJsonString $ValidatedFor
        $new = $verRx.Replace($new, [System.Text.RegularExpressions.MatchEvaluator]{ param($x) $x.Groups[1].Value + $vlit }, 1)
    }

    $parsed = $new | ConvertFrom-Json
    foreach ($key in $Labels.Keys) {
        if ($parsed.SecurityComplianceRuleNames.$key -cne [string]$Labels[$key]) {
            throw "Patched VbrConfig.json does not carry the requested label for $key"
        }
    }
    return $new
}

function Get-ScaChangeLines {
    <# Human-readable before/after lines for a set of label changes. #>
    param($Patchable)
    foreach ($p in @($Patchable)) {
        $verb = if ($p.verdict -eq 'label-changed') { 'changed' } else { 'added' }
        $old = if ($p.currentLabel) { "was: $($p.currentLabel)" } else { 'was: (no entry)' }
        "{0} {1}: {2} ({3})" -f $verb, $p.enum, $p.proposedLabel, $old
    }
}

Export-ModuleMember -Function Get-ScaTriage, Get-ScaRestBestPractices, Merge-ScaTriage, Update-VbrConfigLabels, Get-ScaChangeLines, ConvertTo-ScaVersion
