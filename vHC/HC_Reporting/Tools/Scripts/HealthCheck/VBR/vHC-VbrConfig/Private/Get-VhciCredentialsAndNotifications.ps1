#Requires -Version 5.1

function Get-VhciCredentialsAndNotifications {
    <#
    .Synopsis
        Collects email notification settings and stored credentials (name/username only -
        Veeam cmdlets never expose passwords).
        Exports _EmailNotification.csv, _Credentials.csv.
        Source: Get-VBRConfig.ps1 lines 1435-1457.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting credentials and notification settings..."
    Write-LogFile $message

    $emailNotification = Get-VBRMailNotificationConfiguration
    Write-LogFile "Email notification settings collected"
    $credentials = Get-VBRCredentials |
        Select-Object Name, UserName, Description, CurrentUser, LastModified
    Write-LogFile "Found $(@($credentials).Count) credentials"

    $emailNotification | Export-VhciCsv -FileName '_EmailNotification.csv'
    $credentials       | Export-VhciCsv -FileName '_Credentials.csv'

    Write-LogFile ($message + "DONE")
}
