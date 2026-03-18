#Requires -Version 5.1

function Get-VhcWanAccelerator {
    <#
    .Synopsis
        Collects VBR WAN Accelerator configuration and exports to _WanAcc.csv.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting WAN ACC info..."
    $wan     = $null
    Write-LogFile $message

    try {
        $wan = Get-VBRWANAccelerator
        Write-LogFile ($message + "DONE")
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'WanAccelerator' -ErrorMessage $_.Exception.Message
    }

    $wan | Export-VhciCsv -FileName '_WanAcc.csv'
}
