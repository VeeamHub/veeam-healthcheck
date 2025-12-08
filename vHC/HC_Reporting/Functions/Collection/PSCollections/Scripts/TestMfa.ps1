param(
    [Parameter(Mandatory = $true)]
    [string]$Server,
    [Parameter(Mandatory = $true)]
    [string]$Username,
    [Parameter(Mandatory = $true)]
    [string]$PasswordBase64
)

try {
    Write-Host "[VERBOSE] PowerShell Version: $($PSVersionTable.PSVersion.ToString())"
    # Compare PSModulePath in both contexts
    # Add the Veeam Console directory to PSModulePath
    $veeamConsolePath = "C:\Program Files\Veeam\Backup and Replication\Console"
    if (Test-Path $veeamConsolePath) {
        Write-Verbose "Adding Veeam Console path to PSModulePath: $veeamConsolePath"
        $env:PSModulePath = "$veeamConsolePath;$env:PSModulePath"
    }
    else {
        Write-Error "Veeam Console path not found: $veeamConsolePath"
        exit 1
    }

    Write-Verbose "Attempting to import Veeam.Backup.PowerShell module..."
    Import-Module Veeam.Backup.PowerShell -Force -WarningAction Ignore
    Write-Host "[VERBOSE] Attempting to import Veeam.Backup.PowerShell module..."
    Import-Module Veeam.Backup.PowerShell -Force -WarningAction Ignore
    Write-Host "[VERBOSE] Module imported. Attempting to connect to VBR Server: $Server with user $Username."

    # Decode Base64 password
    $passwordBytes = [System.Convert]::FromBase64String($PasswordBase64)
    $password = [System.Text.Encoding]::UTF8.GetString($passwordBytes)

    Write-Host "[VERBOSE] Password decoded successfully (length: $($password.Length))"
    Write-Host "[VERBOSE] Server: $Server"
    Write-Host "[VERBOSE] Username: $Username"
    Write-Host "[VERBOSE] Password first 5 chars: $($password.Substring(0, [Math]::Min(5, $password.Length)))"
    Write-Host "[VERBOSE] Password last 5 chars: $($password.Substring([Math]::Max(0, $password.Length - 5)))"
    
    # Use -User and -Password parameters directly (same as manual CLI usage)
    # This approach works better for local accounts vs -Credential
    Connect-VBRServer -Server $Server -User $Username -Password $password -ForceAcceptTlsCertificate -ErrorAction Stop
    Write-Host "[VERBOSE] Successfully connected to VBR Server."
    exit 0
}
catch {
    Write-Host "[VERBOSE] Exception occurred: $($_.Exception.Message)"
    exit 1
}
