param(
    [Parameter(Mandatory)]
    [string]$VBRServer,
    [Parameter(Mandatory)]
    [int]$VBRVersion,
    [Parameter(Mandatory = $false)]
    [string]$ReportPath = ""
)

# If ReportPath not provided, use default with server name and timestamp structure
if ([string]::IsNullOrEmpty($ReportPath)) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $ReportPath = "C:\temp\vHC\Original\VBR\$VBRServer\$timestamp"
}

# VMC log path is hardcoded for now. If logs are sent elsewhere, please adjust accordingly.
 $logsPath = "C:\ProgramData\Veeam\Backup\Utils\VMC.log"

 # section identifiers
 $unstrucStart = "=====UNSTRUCTURED DATA===="
  $nasStart = "=====NAS INFRASTRUCTURE===="
 $sectionEnd = "========"
 
 #output files, feel free to rename and relocate
 $csvFilePath = 'Share Breakdown.csv'
 $csv2FilePath = 'output2.csv'
 
 #get file info and set empty containers.
 $content = Get-Content $logsPath
 $sections = @()
 $currentSection = @()
 $capturing = $false
 
 foreach($line in $content){
     if(-not $capturing -and $line -match $unstrucStart){
         $capturing = $true
         $currentSection = @()
         #$currentSection += $line.Remove(0,50)
     }
     elseif(-not $capturing -and $line -match $nasStart){
        $capturing = $true
        $currentSection = @()
        #$currentSection += $line.Remove(0,50)
    }
     elseif($capturing){
         if($line -match $sectionEnd){
             $capturing = $false
             $sections += ,($currentSection)
             $currentSection = @()
         }
         else{
             if( -not $line -match "[VmcStats]"){
                 # Only remove prefix if line is long enough, otherwise use the whole line
                 if ($line.Length -ge 49) {
                     $currentSection += $line.Remove(0,49)
                 } else {
                     $currentSection += $line
                 }
             }
         }
     }
 }
 
 # Here we set a new list to only contain the final data section from the log:
 $dataLines = $sections[$sections.Count-1]
 $data = @()

 # search each line, looking for these strings: TotalObjectStorageSize, NasBackupSourceShareStats, TotalShareSize. Group each into their own list
    $totalObjectStorageSize = @()
    $nasBackupSourceShareStats = @()
    $totalShareSize = @()
    $dataLines | ForEach-Object {
        if($_ -match "TotalObjectStorageSize"){
            $totalObjectStorageSize += $_
        }
        elseif($_ -match "NasBackupSourceShareStats"){
            $nasBackupSourceShareStats += $_
        }
        elseif($_ -match "TotalShareSize"){
            $totalShareSize += $_
        }
    }

 
    # converting to CSV data based on property for readability
    $csvData = $totalObjectStorageSize | ForEach-Object {
        $properties = @{}
        # Split using comma and create key-value pairs
        $_.Trim() -split ', ' | ForEach-Object {
            $key, $value = $_.Split(':', 2).Trim()
            $properties[$key] = $value
        }
        # Output as a PSCustomObject
        [PSCustomObject]$properties
    }
# export to csv
if (!(Test-Path $ReportPath)) { New-Item -Path $ReportPath -ItemType Directory -Force | Out-Null }
$csvData | Export-Csv -Path "$ReportPath\${VBRServer}_NasObjectSourceStorageSize.csv" -NoTypeInformation

#convert $nasBackupSourceShareStats to CSV and export to new csv file
$csvData2 = $nasBackupSourceShareStats | ForEach-Object {
    $properties = @{}
    # Split using comma and create key-value pairs
    $_.Trim() -split ', ' | ForEach-Object {
        $key, $value = $_.Split(':', 2).Trim()
        $properties[$key] = $value
    }
    # Output as a PSCustomObject
    [PSCustomObject]$properties
}
# export to csv
$csvData2 | Export-Csv -Path "$ReportPath\${VBRServer}_NasFileData.csv" -NoTypeInformation

#convert $totalShareSize to CSV and export to new csv file
$csvData3 = $totalShareSize | ForEach-Object {
    $properties = @{}
    # Split using comma and create key-value pairs
    $_.Trim() -split ', ' | ForEach-Object {
        $key, $value = $_.Split(':', 2).Trim()
        $properties[$key] = $value
    }
    # Output as a PSCustomObject
    [PSCustomObject]$properties
}
# export to csv
$csvData3 | Export-Csv -Path "$ReportPath\${VBRServer}_NasSharesize.csv" -NoTypeInformation