#Requires -Version 5.1

function Get-VhciRestDbType {
    <#
    .Synopsis
        Reads the VBR configuration database vendor from REST GET /api/v1/serverInfo and
        normalises it to the registry vocabulary used in _vbrinfo.csv: 'MsSql' or 'PostgreSql'.
    .Description
        Remote runs have no local registry, so the registry-based DbType is empty. Built on the
        module's shared REST client (Connect-VhciRestApi / Invoke-VhciRestApi): one token request
        per run, so a wrong password costs one failed login. Returns $null, with a WARNING log
        line, on any failure or an unrecognised vendor; the caller then leaves DbType empty and
        the report shows "Unknown".
    .Parameter Server
        VBR server host name or address.
    .Parameter Credential
        VBR account used for the REST token request.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [string] $Server,
        [Parameter(Mandatory)] [System.Management.Automation.PSCredential] $Credential
    )

    try {
        $session = Connect-VhciRestApi -Server $Server -Credential $Credential -TimeoutSec 15
        $info    = Invoke-VhciRestApi -Session $session -Path '/api/v1/serverInfo' -TimeoutSec 15
    } catch {
        Write-LogFile "Get-VhciRestDbType: REST serverInfo lookup failed: $($_.Exception.Message)" -LogLevel "WARNING"
        return $null
    }

    $vendor = if ($info -and $info.PSObject.Properties['databaseVendor']) { [string]$info.databaseVendor } else { '' }
    switch -Regex ($vendor) {
        '^(?i)(MsSql|MicrosoftSql|SqlServer|Microsoft SQL Server)$' { return 'MsSql' }
        '^(?i)(PostgreSql|Postgres)$'                               { return 'PostgreSql' }
    }
    Write-LogFile "Get-VhciRestDbType: serverInfo databaseVendor '$vendor' not recognised" -LogLevel "WARNING"
    return $null
}
