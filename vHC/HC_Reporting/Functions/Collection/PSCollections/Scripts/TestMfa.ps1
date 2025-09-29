param(
    [Parameter(Mandatory=$true)]
    [string]$Server,
    [Parameter(Mandatory=$true)]
    [string]$Username,
    [Parameter(Mandatory=$true)]
    [string]$Password
)

try {
    WRite-Host($PSVersionTable.PSVersion.ToString())
    #Write-Host(Get-Module -ListAvailable )
    Import-Module Veeam.Backup.PowerShell  -Force -WarningAction Ignore
    Connect-VBRServer -Server $Server -User $Username -Password $Password -ErrorAction Stop
    exit 0
} catch {
    Write-Host $_.Exception.Message
    exit 1
}
