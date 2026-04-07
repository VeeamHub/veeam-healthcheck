#Requires -Version 5.1

function Get-VhcMajorVersion {
    <#
    .Synopsis
        Detects the installed VBR major version number.
        For v13+: reads from Get-VBRBackupServerInfo.Build.Major.
        For v10-12: reads Veeam.Backup.Core.dll ProductVersion from the CorePath registry key.
    .Outputs
        [int] Major version number (10, 11, 12, or 13). Returns 0 if detection fails.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param()

    try {
        $backupServer = Get-VBRBackupServerInfo
        $buildMajor   = [int]$backupServer.Build.Major
    } catch {
        Write-LogFile "Get-VhcMajorVersion: failed to call Get-VBRBackupServerInfo: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'VbrInfo' -ErrorMessage $_.Exception.Message
        return 0
    }

    # v13+: BackupServerInfo.Build.Major is authoritative
    if ($buildMajor -ge 13) {
        Write-LogFile "Detected VBR version: $($backupServer.Build) (major: $buildMajor)"
        return $buildMajor
    }

    # v10-12: read from Veeam.Backup.Core.dll ProductVersion via registry CorePath
    try {
        $corePath   = Get-ItemProperty -Path "HKLM:\Software\Veeam\Veeam Backup and Replication\" `
                                       -Name "CorePath" -ErrorAction Stop
        $dllPath    = Join-Path -Path $corePath.CorePath -ChildPath "Veeam.Backup.Core.dll" -Resolve -ErrorAction Stop
        $dllFile    = Get-Item -Path $dllPath -ErrorAction Stop
        $productVer = $dllFile.VersionInfo.ProductVersion
        $majorStr   = $productVer.Split('.')[0]
        $major      = [int]$majorStr

        Write-LogFile "Detected VBR version: $productVer (major: $major)"

        if ($major -in 10, 11, 12) {
            return $major
        }

        Write-LogFile "Unknown VBR major version '$major' from DLL - returning 0" -LogLevel "WARNING"
        return 0
    } catch {
        Write-LogFile "Get-VhcMajorVersion: registry/DLL detection failed: $($_.Exception.Message)" -LogLevel "WARNING"
        # Fall back to whatever BackupServerInfo reported
        if ($buildMajor -gt 0) {
            Write-LogFile "Falling back to BackupServerInfo major version: $buildMajor" -LogLevel "WARNING"
            return $buildMajor
        }
        Add-VhciModuleError -CollectorName 'VbrInfo' -ErrorMessage $_.Exception.Message
        return 0
    }
}
