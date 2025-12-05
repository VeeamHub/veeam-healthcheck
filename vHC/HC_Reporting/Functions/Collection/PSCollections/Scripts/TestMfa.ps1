param(
    [Parameter(Mandatory=$true)]
    [string]$Server,
    [Parameter(Mandatory=$true)]
    [string]$Username,
    [Parameter(Mandatory=$true)]
    [string]$Password
)

try {
    Write-Host "[VERBOSE] PowerShell Version: $($PSVersionTable.PSVersion.ToString())"
    # Compare PSModulePath in both contexts
# Add the Veeam Console directory to PSModulePath
$veeamConsolePath = "C:\Program Files\Veeam\Backup and Replication\Console"
if (Test-Path $veeamConsolePath) {
    Write-Verbose "Adding Veeam Console path to PSModulePath: $veeamConsolePath"
    $env:PSModulePath = "$veeamConsolePath;$env:PSModulePath"
} else {
    Write-Error "Veeam Console path not found: $veeamConsolePath"
    exit 1
}

Write-Verbose "Attempting to import Veeam.Backup.PowerShell module..."
Import-Module Veeam.Backup.PowerShell -Force -WarningAction Ignore
    Write-Host "[VERBOSE] Attempting to import Veeam.Backup.PowerShell module..."
    Import-Module Veeam.Backup.PowerShell  -Force -WarningAction Ignore
    Write-Host "[VERBOSE] Module imported. Attempting to connect to VBR Server: $Server with user $Username."

    # Convert password to SecureString for better handling of special characters
    $securePassword = ConvertTo-SecureString -String $Password -AsPlainText -Force
    $credential = New-Object System.Management.Automation.PSCredential($Username, $securePassword)

    Connect-VBRServer -Server $Server -Credential $credential -ForceAcceptTlsCertificate -ErrorAction Stop
    Write-Host "[VERBOSE] Successfully connected to VBR Server."
    exit 0
} catch {
    Write-Host "[VERBOSE] Exception occurred: $($_.Exception.Message)"
    exit 1
}
