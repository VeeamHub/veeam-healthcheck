cd C:\Users\adam\Source\Repos\VeeamHub\veeam-healthcheck\vHC\HC_Reporting\Resources\VB365_Localization
$source = Get-Content "vb365res.txt"
$output = "test.csv"

$null | Export-Csv test.csv -NoTypeInformation

foreach($s in $source){
    if(!$s.StartsWith("#")){
        if($s -ne ""){
            $s = $s.Split("=")
            $seq = @($s[0],$s[1])
            $item = New-Object psobject
            $item | Add-Member NoteProperty Name $s[0]
            $item | Add-Member NoteProperty Value $s[1]
            $item | Export-Csv test.csv -Append -NoTypeInformation
        }
 }
}
