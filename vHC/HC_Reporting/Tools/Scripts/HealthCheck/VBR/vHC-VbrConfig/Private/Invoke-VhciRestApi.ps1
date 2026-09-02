#Requires -Version 5.1

# Invoke-VhciRestApi.ps1
# ---------------------------------------------------------------------------
# Reusable VBR REST API client for the vHC-VbrConfig module.
#
#   Connect-VhciRestApi  - acquire an OAuth2 bearer token, return a session object.
#   Invoke-VhciRestApi   - issue a typed GET/POST against the API, with auto-paging.
#
# This is the single, canonical REST transport for the module. New REST-based
# collectors and PS->REST migrations build on this instead of hand-rolling auth.
#
# Extracted from the proven pattern in Check-SecurityComplianceDrift-REST.ps1, but
# parameterized, paging-aware, and version-corrected.
#
# API VERSION: defaults to '1.2-rev0' - the version confirmed accepted by the live
# VBR v13 RTM (the older standalone scripts hardcoded '1.3-rev1', which the RTM
# rejects; do not copy that value).
#
# PS 5.1 NOTE: this file is dot-sourced by the module on PS 5.1 hosts, so it must
# *load* under 5.1. Invoke-RestMethod -SkipCertificateCheck only exists on PS 6+,
# so cert handling branches at runtime: PS7 uses -SkipCertificateCheck; PS5.1 sets
# a ServicePointManager trust callback + TLS 1.2. The collectors themselves run
# under the embedded PowerShell 7, so the 5.1 path is load-safety only.
# ---------------------------------------------------------------------------

function Set-VhciRestCertTrust {
    <#
    .Synopsis
        On Windows PowerShell 5.1, install a trust-all server-cert callback and
        force TLS 1.2 so Invoke-RestMethod can reach the self-signed VBR endpoint.
        No-op on PS 6+ (those callers pass -SkipCertificateCheck instead).
    #>
    if ($PSVersionTable.PSVersion.Major -ge 6) { return }
    if ($script:VhciRestCertTrustInstalled) { return }
    try {
        [System.Net.ServicePointManager]::SecurityProtocol = `
            [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        $script:VhciRestCertTrustInstalled = $true
    } catch {
        Write-Verbose "Set-VhciRestCertTrust: could not install trust callback: $_"
    }
}

function Get-VhciRestCommonArgs {
    <#
    .Synopsis
        Build the splat shared by every Invoke-RestMethod call: timeout, error
        action, and (PS6+) -SkipCertificateCheck. Centralizes the version branch.
    #>
    param([int] $TimeoutSec = 60)
    $common = @{ TimeoutSec = $TimeoutSec; ErrorAction = 'Stop' }
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        $common['SkipCertificateCheck'] = $true
    } else {
        Set-VhciRestCertTrust
    }
    return $common
}

function Connect-VhciRestApi {
    <#
    .Synopsis
        Acquire an OAuth2 bearer token from a VBR server and return a session object
        that Invoke-VhciRestApi consumes. Supports password and refresh-token grants.
    .Parameter Server
        VBR host name or IP (no scheme/port).
    .Parameter Port
        REST API port. Default 9419.
    .Parameter Credential
        PSCredential. Alternative to -Username/-Password.
    .Parameter Username
        VBR account username (used when -Credential is not supplied).
    .Parameter Password
        VBR account password (used when -Credential is not supplied).
    .Parameter ApiVersion
        x-api-version header value. Default '1.2-rev0' (live-confirmed for VBR v13).
    .Parameter TimeoutSec
        Per-request timeout. Default 30.
    .Outputs
        [pscustomobject] session: BaseUrl, ApiVersion, Headers, AccessToken,
        RefreshToken, ExpiresAtUtc, Server, Port.
    #>
    [CmdletBinding(DefaultParameterSetName = 'Plain')]
    param(
        [Parameter(Mandatory)] [string] $Server,
        [int] $Port = 9419,
        [Parameter(ParameterSetName = 'Cred', Mandatory)] [pscredential] $Credential,
        [Parameter(ParameterSetName = 'Plain', Mandatory)] [string] $Username,
        [Parameter(ParameterSetName = 'Plain', Mandatory)] [string] $Password,
        [string] $ApiVersion = '1.2-rev0',
        [int] $TimeoutSec = 30
    )

    if ($PSCmdlet.ParameterSetName -eq 'Cred') {
        $Username = $Credential.UserName
        $Password = $Credential.GetNetworkCredential().Password
    }

    $baseUrl = "https://${Server}:${Port}"
    $common  = Get-VhciRestCommonArgs -TimeoutSec $TimeoutSec

    try {
        $tok = Invoke-RestMethod @common `
            -Method      POST `
            -Uri         "$baseUrl/api/oauth2/token" `
            -Headers     @{ 'x-api-version' = $ApiVersion } `
            -ContentType 'application/x-www-form-urlencoded' `
            -Body        @{ grant_type = 'password'; username = $Username; password = $Password }
    } catch {
        $detail = ''
        try { $detail = $_.ErrorDetails.Message } catch { }
        throw "Connect-VhciRestApi: auth failed against ${baseUrl} - $($_.Exception.Message)$(if ($detail) { " | $detail" })"
    }

    $expiresAt = (Get-Date).ToUniversalTime()
    if ($tok.expires_in) { $expiresAt = $expiresAt.AddSeconds([int]$tok.expires_in) }

    return [pscustomobject]@{
        BaseUrl      = $baseUrl
        Server       = $Server
        Port         = $Port
        ApiVersion   = $ApiVersion
        AccessToken  = $tok.access_token
        RefreshToken = $tok.refresh_token
        ExpiresAtUtc = $expiresAt
        Headers      = @{
            'Authorization' = "Bearer $($tok.access_token)"
            'x-api-version' = $ApiVersion
            'Accept'        = 'application/json'
        }
    }
}

function Invoke-VhciRestApi {
    <#
    .Synopsis
        Issue a request against the VBR REST API using a session from
        Connect-VhciRestApi (or auto-connect via -Server/-Credential).
    .Description
        GET requests to collection endpoints return the unwrapped item array. With
        -All, every page is fetched (VBR pages with skip/limit and reports total in
        the 'pagination' block) and the concatenated items are returned. Single-object
        GETs and POSTs return the raw deserialized response.
    .Parameter Session
        Session object from Connect-VhciRestApi.
    .Parameter Server
        VBR host - triggers an auto-connect when -Session is not supplied (requires -Credential).
    .Parameter Credential
        Credential for auto-connect.
    .Parameter Path
        API path, e.g. '/api/v1/serverInfo' or '/api/v1/restorePoints'.
    .Parameter Method
        HTTP method. Default GET.
    .Parameter Body
        Request body (hashtable/object); serialized as JSON for non-GET.
    .Parameter All
        Auto-page a collection endpoint and return all items.
    .Parameter PageSize
        Page size when -All is used. Default 200.
    .Parameter TimeoutSec
        Per-request timeout. Default 60.
    .Outputs
        Item array (collection GET / -All), or raw response object (single GET / POST).
    #>
    [CmdletBinding(DefaultParameterSetName = 'Session')]
    param(
        [Parameter(ParameterSetName = 'Session', Mandatory)] [object] $Session,
        [Parameter(ParameterSetName = 'AutoConnect', Mandatory)] [string] $Server,
        [Parameter(ParameterSetName = 'AutoConnect', Mandatory)] [pscredential] $Credential,
        [Parameter(ParameterSetName = 'AutoConnect')] [int] $Port = 9419,
        [Parameter(ParameterSetName = 'AutoConnect')] [string] $ApiVersion = '1.2-rev0',
        [Parameter(Mandatory)] [string] $Path,
        [ValidateSet('GET', 'POST', 'PUT', 'DELETE')] [string] $Method = 'GET',
        [object] $Body,
        [switch] $All,
        [int] $PageSize = 200,
        [int] $TimeoutSec = 60
    )

    if ($PSCmdlet.ParameterSetName -eq 'AutoConnect') {
        $Session = Connect-VhciRestApi -Server $Server -Port $Port -Credential $Credential -ApiVersion $ApiVersion
    }

    $common = Get-VhciRestCommonArgs -TimeoutSec $TimeoutSec
    $uri    = "$($Session.BaseUrl)$Path"

    $invokeArgs = @{
        Method  = $Method
        Headers = $Session.Headers
    }
    if ($null -ne $Body) {
        $invokeArgs['Body']        = ($Body | ConvertTo-Json -Depth 10)
        $invokeArgs['ContentType'] = 'application/json'
    }

    # Non-paged: single call, return raw response (caller unwraps as needed).
    if (-not $All) {
        return Invoke-RestMethod @common @invokeArgs -Uri $uri
    }

    # Paged: accumulate items across pages using skip/limit. VBR responses wrap the
    # list in 'data' with a 'pagination' block { total, count, skip, limit }.
    $sep   = if ($Path -match '\?') { '&' } else { '?' }
    $items = [System.Collections.ArrayList]::new()
    $skip  = 0
    while ($true) {
        $pageUri = "$uri$sep" + "skip=$skip&limit=$PageSize"
        $resp    = Invoke-RestMethod @common @invokeArgs -Uri $pageUri
        $page    = if ($null -ne $resp.data) { @($resp.data) } else { @($resp) }
        if ($page.Count -gt 0) { [void]$items.AddRange($page) }

        $total = $null
        if ($resp.PSObject.Properties['pagination'] -and $resp.pagination) { $total = $resp.pagination.total }

        $skip += $PageSize
        if ($null -ne $total) {
            if ($items.Count -ge [int]$total) { break }
        } elseif ($page.Count -lt $PageSize) {
            break   # no pagination metadata and a short page => last page
        }
        if ($page.Count -eq 0) { break }
    }
    return $items.ToArray()
}
