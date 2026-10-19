#Requires -Version 5.1

function Get-VhciRestDbType {
    <#
    .Synopsis
        Reads the VBR configuration database vendor from REST GET /api/v1/serverInfo and
        normalises it to the registry vocabulary used in _vbrinfo.csv: 'MsSql' or 'PostgreSql'.
    .Description
        Remote runs have no local registry, so the registry-based DbType is empty. The REST API
        reports databaseVendor without registry access. The password is sent in the form body of
        the token request, never in a URL. Returns $null when REST is unreachable, the credential
        is rejected, or the vendor is not one the report knows; the caller then leaves DbType empty
        and the report shows "Unknown".
    .Parameter Server
        VBR server host name or address.
    .Parameter Credential
        VBR account used for the REST token request.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [string] $Server,
        [Parameter(Mandatory)] [System.Management.Automation.PSCredential] $Credential,
        [int] $Port = 9419,
        [int] $TimeoutSec = 15,
        [string[]] $ApiVersions = @('1.2-rev0', '1.1-rev2', '1.1-rev1', '1.1-rev0')
    )

    $base = "https://${Server}:${Port}"

    # Windows PowerShell 5.1 has no -SkipCertificateCheck; VBR ships a self-signed REST certificate.
    $common = @{ TimeoutSec = $TimeoutSec; ErrorAction = 'Stop' }
    $restoreCallback = $false
    $previousCallback = $null
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        $common['SkipCertificateCheck'] = $true
    } else {
        $previousCallback = [System.Net.ServicePointManager]::ServerCertificateValidationCallback
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12
        $restoreCallback = $true
    }

    try {
        foreach ($ver in $ApiVersions) {
            $headers = @{ 'x-api-version' = $ver }
            try {
                $token = Invoke-RestMethod @common -Method POST -Uri "$base/api/oauth2/token" -Headers $headers `
                    -ContentType 'application/x-www-form-urlencoded' `
                    -Body @{
                        grant_type = 'password'
                        username   = $Credential.UserName
                        password   = $Credential.GetNetworkCredential().Password
                    }
            } catch {
                Write-LogFile "Get-VhciRestDbType: token request failed for API $ver ($($_.Exception.GetType().Name))" -LogLevel "WARNING"
                continue
            }

            try {
                $info = Invoke-RestMethod @common -Uri "$base/api/v1/serverInfo" `
                    -Headers @{ 'x-api-version' = $ver; 'Authorization' = "Bearer $($token.access_token)" }
            } catch {
                Write-LogFile "Get-VhciRestDbType: serverInfo request failed for API $ver ($($_.Exception.GetType().Name))" -LogLevel "WARNING"
                continue
            }

            $vendor = if ($info -and $info.PSObject.Properties['databaseVendor']) { [string]$info.databaseVendor } else { '' }
            switch -Regex ($vendor) {
                '^(?i)(MsSql|MicrosoftSql|SqlServer|Microsoft SQL Server)$' { return 'MsSql' }
                '^(?i)(PostgreSql|Postgres|PostgreSQL)$'                    { return 'PostgreSql' }
            }
            Write-LogFile "Get-VhciRestDbType: serverInfo databaseVendor '$vendor' not recognised" -LogLevel "WARNING"
            return $null
        }
        return $null
    } finally {
        if ($restoreCallback) {
            [System.Net.ServicePointManager]::ServerCertificateValidationCallback = $previousCallback
        }
    }
}
