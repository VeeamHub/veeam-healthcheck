#Requires -Version 5.1

enum LogLevel {
    TRACE
    PROFILE
    DEBUG
    INFO
    WARNING
    ERROR
    FATAL
}

function Write-LogFile {
    [CmdletBinding()]
    param (
        [string]$Message,
        # "Warnings" added to fix source bug (was missing from ValidateSet; called at lines 1251 & 1522)
        [ValidateSet("Main", "Errors", "NoResult", "Warnings", "VeeamConfigScript")][string]$LogName = "VeeamConfigScript",
        [ValidateSet("TRACE", "PROFILE", "DEBUG", "INFO", "WARNING", "ERROR", "FATAL")][string]$LogLevel = "INFO"
    )
    begin {}
    process {
        if (-not $PSBoundParameters.ContainsKey('Message') -or [string]::IsNullOrWhiteSpace($Message)) {
            Write-Warning "Write-LogFile called with no message. Skipping log write."
            return
        }
        $effectiveLogLevel = if ($script:LogLevel) { $script:LogLevel } else { "INFO" }
        if ([LogLevel]$LogLevel -ge [LogLevel]$effectiveLogLevel) {
            $logBase = if ($script:LogPath) { $script:LogPath.TrimEnd('\') } else { "C:\temp\vHC\Original\Log" }
            $outPath = $logBase + "\Collector" + $LogName + ".log"
            $dir = Split-Path -Path $outPath -Parent
            if (-not (Test-Path $dir)) { New-Item -Path $dir -ItemType Directory -Force | Out-Null }
            $line = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss") + "`t" + $LogLevel + "`t`t" + $Message
            Add-Content -Path $outPath -Value $line -Encoding UTF8
            Write-Host "[LOG:$LogLevel] $Message"
        }
    }
    end {}
}
