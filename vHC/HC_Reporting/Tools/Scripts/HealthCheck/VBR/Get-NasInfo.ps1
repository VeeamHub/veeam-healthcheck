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
                 $currentSection += $line.Remove(0,49)
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
$csvData | Export-Csv -Path "C:\Temp\vHC\Original\VBR\localhost_NasObjectSourceStorageSize.csv" -NoTypeInformation

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
$csvData2 | Export-Csv -Path "C:\Temp\vHC\Original\VBR\localhost_NasFileData.csv" -NoTypeInformation

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
$csvData3 | Export-Csv -Path "C:\Temp\vHC\Original\VBR\localhost_NasSharesize.csv" -NoTypeInformation