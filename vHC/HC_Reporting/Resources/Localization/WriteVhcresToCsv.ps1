$source = Get-Content "vhcres.txt"
$output = "vhcres.csv"


$header = "DO NOT TOUCH, Translate this column only"
$header | Export-Csv $output -NoTypeInformation

foreach($s in $source){
    if(!$s.StartsWith("#") -and $s -ne $null){
    $seq = $s.Replace("=", ",")
    $seq | Export-Csv $output -Append -NoTypeInformation
    #write-host($seq)
 }
}