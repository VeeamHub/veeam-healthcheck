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
# In your manual session:
$env:PSModulePath -split ';'

# In the runner context, add this to your script:
Write-Host "PSModulePath: $($env:PSModulePath)"
    Write-Host "[VERBOSE] Attempting to import Veeam.Backup.PowerShell module..."
    #Write-Host(Get-Module -ListAvailable )
    Import-Module Veeam.Backup.PowerShell  -Force -WarningAction Ignore
    Write-Host "[VERBOSE] Module imported. Attempting to connect to VBR Server: $Server with user $Username."
    Connect-VBRServer -Server $Server -User $Username -Password $Password -ForceAcceptTlsCertificate -ErrorAction Stop
    Write-Host "[VERBOSE] Successfully connected to VBR Server."
    exit 0
} catch {
    Write-Host "[VERBOSE] Exception occurred: $($_.Exception.Message)"
    exit 1
}
