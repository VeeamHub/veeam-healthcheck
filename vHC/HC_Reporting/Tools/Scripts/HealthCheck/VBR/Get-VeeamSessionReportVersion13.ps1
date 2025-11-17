

#region --- Veeam login mechanism ---

param(
	[Parameter(Mandatory)]
	[string]$VBRServer,
	[Parameter(Mandatory=$false)]
	[string]$User = "",
	[Parameter(Mandatory=$false)]
	[string]$Password = "",
	[Parameter(Mandatory=$false)]
	[int]$ReportInterval = 7
)

function Write-LogFile {
	param(
		[string]$Message,
		[string]$LogName = "Main",
		[string]$LogLevel = "INFO"
	)
	$outPath = "C:\\temp\\vHC\\Original\\Log\\VeeamSessionReport.log"
	$line = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss") + "`t" + $LogLevel + "`t`t" + $Message
	Add-Content -Path $outPath -Value $line -Encoding UTF8
}


try {
	Disconnect-VBRServer -ErrorAction SilentlyContinue
	if ([string]::IsNullOrEmpty($User) -or [string]::IsNullOrEmpty($Password)) {
		# Connect without credentials (local/Windows authentication)
		Connect-VBRServer -Server $VBRServer
		Write-LogFile "Connected to VBR Server: $VBRServer (Windows Authentication)"
	} else {
		# Connect with provided credentials (remote)
		Connect-VBRServer -Server $VBRServer -User $User -Password $Password
		Write-LogFile "Connected to VBR Server: $VBRServer (Credential Authentication)"
	}
} catch {
	Write-LogFile "Failed to connect to VBR Server: $VBRServer. Error: $($_.Exception.Message)" "Errors" "ERROR"
	exit
}
#endregion


# Calculate cutoff date (ReportInterval + 1 days ago)
$cutoff = (Get-Date).AddDays(-1 * ($ReportInterval + 1))
$sessions = Get-VBRBackupSession | Where-Object { $_.Info.CreationTime -ge $cutoff }
$output = @()
$i = 1
foreach ($session in $sessions) {
    Write-LogFile "Processing session $i of $($sessions.Count): Job '$($session.JobName)', VM '$($session.Name)'"
    $i++


	$Bottleneck = $session.Logger.GetLog().UpdatedRecords | Where-Object Title -Match "Load"
	$BottleneckDetails = if ($Bottleneck) { $Bottleneck.Title -replace 'Load: ', '' } else { '' }

	$PrimaryBottleneckDetails = $session.Logger.GetLog().UpdatedRecords | Where-Object Title -Match "Primary Bottleneck"
	$PrimaryBottleneck = if ($PrimaryBottleneckDetails) { $PrimaryBottleneckDetails.Title -replace 'Primary bottleneck: ', '' } else { '' }

	$obj = [PSCustomObject]@{
		JobName            = $session.JobName
		VMName             = $session.Name
		Status             = $session.Result
		IsRetry            = $session.Info.IsRetryMode
		ProcessingMode     = $session.ProcessingMode
		JobDuration        = $session.Progress.Duration.ToString()
		TaskDuration       = $session.WorkDetails.WorkDuration.ToString()
		TaskAlgorithm      = $session.Info.SessionAlgorithm
		CreationTime       = $session.Info.CreationTime
		BackupSizeGB       = [math]::Round($session.BackupStats.BackupSize / 1GB, 2)
		DataSizeGB         = [math]::Round($session.BackupStats.DataSize / 1GB, 2)
		DedupRatio         = $session.BackupStats.DedupRatio
		CompressRatio      = $session.BackupStats.CompressRatio
		BottleneckDetails  = $BottleneckDetails
		PrimaryBottleneck  = $PrimaryBottleneck
		JobType            = $session.Platform.Platform
	}
	$output += $obj
}

$output | Export-Csv -Path "C:\\temp\\vHC\\Original\\VBR\\VeeamSessionReport.csv" -NoTypeInformation -Encoding UTF8

