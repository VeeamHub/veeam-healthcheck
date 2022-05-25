cd C:\Users\cac89\Source\Repos\VeeamHub\veeam-healthcheck\vHC\HC_Reporting\Resources\Localization
$content = Get-Content -LiteralPath "vhcres.txt"
echo $null | out-file pubstrings.txt

foreach($line in $content){
    if(!$line.StartsWith("#")){

        $split = $line.Split()
        if($split -ne $null){
        
            if($split[0] -ne "#"){
                $string = "public static string " + $split[0] + " = m4.GetString(`"" + $split[0] + "`");"
                
                Write-Host($string)
                $string | Out-File -Append pubstrings.txt

            }
        }
    }
}