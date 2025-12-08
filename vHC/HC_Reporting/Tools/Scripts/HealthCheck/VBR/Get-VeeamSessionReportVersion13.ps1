

#region --- Veeam login mechanism ---

param(
	[Parameter(Mandatory)]
	[string]$VBRServer,
	[Parameter(Mandatory = $false)]
	[string]$User = "",
	[Parameter(Mandatory = $false)]
	[string]$Password = "",
	[Parameter(Mandatory = $false)]
	[string]$PasswordBase64 = "",
	[Parameter(Mandatory = $false)]
	[int]$ReportInterval = 7,
	[Parameter(Mandatory = $false)]
	[string]$ReportPath = ""
)

# If ReportPath not provided, use default with server name and timestamp structure
if ([string]::IsNullOrEmpty($ReportPath)) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $ReportPath = "C:\temp\vHC\Original\VBR\$VBRServer\$timestamp"
}

function Write-LogFile {
	param(
		[string]$Message,
		[string]$LogName = "Main",
		[string]$LogLevel = "INFO"
	)
	$outPath = "C:\\temp\\vHC\\Original\\Log\\VeeamSessionReport.log"
	# Ensure directory exists
	$logDir = Split-Path -Path $outPath -Parent
	if (-not (Test-Path $logDir)) {
		New-Item -Path $logDir -ItemType Directory -Force | Out-Null
	}
	$line = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss") + "`t" + $LogLevel + "`t`t" + $Message
	Add-Content -Path $outPath -Value $line -Encoding UTF8
}


try {
	Disconnect-VBRServer -ErrorAction SilentlyContinue
	if ([string]::IsNullOrEmpty($User) -or [string]::IsNullOrEmpty($PasswordBase64)) {
		# Connect without credentials (local/Windows authentication)
		Connect-VBRServer -Server $VBRServer
		Write-LogFile "Connected to VBR Server: $VBRServer (Windows Authentication)"
	}
 else {
		# Connect with provided credentials (remote)
		# Decode Base64 password
		$passwordBytes = [System.Convert]::FromBase64String($PasswordBase64)
		$password = [System.Text.Encoding]::UTF8.GetString($passwordBytes)
        
		# Convert to SecureString and create credential
		$securePassword = ConvertTo-SecureString -String $password -AsPlainText -Force
		$credential = New-Object System.Management.Automation.PSCredential($User, $securePassword)
		Connect-VBRServer -Server $VBRServer -Credential $credential
		Write-LogFile "Connected to VBR Server: $VBRServer (Credential Authentication)"
	}
}
catch {
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
		JobName           = $session.JobName
		VMName            = $session.Name
		Status            = $session.Result
		IsRetry           = $session.Info.IsRetryMode
		ProcessingMode    = $session.ProcessingMode
		JobDuration       = $session.Progress.Duration.ToString()
		TaskDuration      = $session.WorkDetails.WorkDuration.ToString()
		TaskAlgorithm     = $session.Info.SessionAlgorithm
		CreationTime      = $session.Info.CreationTime
		BackupSizeGB      = [math]::Round($session.BackupStats.BackupSize / 1GB, 2)
		DataSizeGB        = [math]::Round($session.BackupStats.DataSize / 1GB, 2)
		DedupRatio        = $session.BackupStats.DedupRatio
		CompressRatio     = $session.BackupStats.CompressRatio
		BottleneckDetails = $BottleneckDetails
		PrimaryBottleneck = $PrimaryBottleneck
		JobType           = $session.Platform.Platform
	}
	$output += $obj
}

if (-not (Test-Path $ReportPath)) {
	New-Item -Path $ReportPath -ItemType Directory -Force | Out-Null
}

$csvPath = Join-Path -Path $ReportPath -ChildPath "VeeamSessionReport.csv"
$output | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
Write-LogFile "Exported $($output.Count) sessions to $csvPath"