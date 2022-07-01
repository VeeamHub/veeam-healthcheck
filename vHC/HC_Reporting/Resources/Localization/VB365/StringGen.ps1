#Write-Host(Get-Location)
#$input = Read-Host("hit a key")
#cd C:\Users\Administrator\Source\Repos\VeeamHub\veeam-healthcheck\vHC\HC_Reporting\Resources\Localization\VB365
$loc = Get-Location

& 'C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\Tools\Launch-VsDevShell.ps1'
start-sleep -Seconds 2

cd $loc
$files = gci -Name "vb365_vhcres*.txt"
foreach($f in $files){
 ResGen.exe $f
}

$content = Get-Content -LiteralPath "vb365_vhcres.txt"


echo $null | out-file .\Vb365ResourceHandler.cs
echo $null | out-file pubstrings.txt

"using System.Resources;`nnamespace VeeamHealthCheck.Resources.Localization.VB365`n{`n`nclass Vb365ResourceHandler`n{private static ResourceManager vb365res = new(`"VeeamHealthCheck.Resources.Localization.VB365.vb365_vhcres`", typeof(ResourceHandler).Assembly);`n" | out-file .\Vb365ResourceHandler.cs

foreach($line in $content){
    if(!$line.StartsWith("#")){

        $split = $line.Split()
        if($split -ne $null){
        
            if($split[0] -ne "#"){
                $string = "public static string " + $split[0] + " = vb365res.GetString(`"" + $split[0] + "`");"
                
                #Write-Host($string)
                $string | Out-File -Append pubstrings.txt
                $string | Out-File -Append .\Vb365ResourceHandler.cs
            }
        }
    }
}
"}}" | Out-File -Append .\Vb365ResourceHandler.cs