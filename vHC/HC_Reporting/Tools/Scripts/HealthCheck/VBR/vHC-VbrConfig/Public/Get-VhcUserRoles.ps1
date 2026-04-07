#Requires -Version 5.1

function Get-VhcUserRoles {
    <#
    .Synopsis
        Collects VBR user role assignments and exports them to _UserRoles.csv.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting user role assignments..."
    Write-LogFile $message

    try {
        Get-VBRUserRoleAssignment | Export-VhciCsv -FileName '_UserRoles.csv'
        Write-LogFile ($message + "DONE")
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'UserRoles' -ErrorMessage $_.Exception.Message
    }
}
