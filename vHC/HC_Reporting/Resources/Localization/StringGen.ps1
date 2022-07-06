#cd C:\Users\cac89\Source\Repos\VeeamHub\veeam-healthcheck\vHC\HC_Reporting\Resources\Localization
$loc = Get-Location

& 'C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\Tools\Launch-VsDevShell.ps1'
start-sleep -Seconds 2

cd $loc
$files = gci -Name "vhcres*.txt"
foreach($f in $files){
 ResGen.exe $f
}

$content = Get-Content -LiteralPath "vhcres.txt"


echo $null | out-file .\ResourceHandler.cs
echo $null | out-file pubstrings.txt

"using System.Resources;`nnamespace VeeamHealthCheck`n{`n`nclass ResourceHandler`n{private static ResourceManager m4 = new(`"VeeamHealthCheck.Resources.Localization.vhcres`", typeof(ResourceHandler).Assembly);`n" | out-file .\ResourceHandler.cs

foreach($line in $content){
    if(!$line.StartsWith("#")){

        $split = $line.Split()
        if($split -ne $null){
        
            if($split[0] -ne "#"){
                $string = "public static string " + $split[0] + " = m4.GetString(`"" + $split[0] + "`");"
                
                #Write-Host($string)
                $string | Out-File -Append pubstrings.txt
                $string | out-file -Append .\ResourceHandler.cs
            }
        }
    }
}
"}}" | out-file -Append .\ResourceHandler.cs