#Requires -Version 5.1

function Get-VhcRegistrySettings {
    <#
    .Synopsis
        Collects Veeam registry settings from HKLM and exports to _regkeys.csv.
        Skipped when -RemoteExecution is $true (registry not accessible on remote agents).
    .Parameter RemoteExecution
        When $true, collection is skipped and no CSV is written.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [bool]$RemoteExecution = $false
    )

    if ($RemoteExecution -eq $true) {
        Write-LogFile "Skipping registry collection - RemoteExecution is enabled." -LogLevel "INFO"
        return
    }

    $message = "Collecting Registry info..."
    $output  = $null
    Write-LogFile $message

    try {
        $reg = Get-Item "HKLM:\SOFTWARE\Veeam\Veeam Backup and Replication"

        [System.Collections.ArrayList]$output = @()
        foreach ($r in $reg.Property) {
            $regout2 = [pscustomobject][ordered]@{
                'KeyName' = $r
                'Value'   = $reg.GetValue($r)
            }
            $null = $output.Add($regout2)
        }

        Write-LogFile ($message + "DONE")
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'RegistrySettings' -ErrorMessage $_.Exception.Message
    }

    $output | Export-VhciCsv -FileName '_regkeys.csv'
}
