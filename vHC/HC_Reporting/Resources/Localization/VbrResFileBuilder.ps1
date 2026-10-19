# WARNING: this script regenerates vhcres*.resx FROM the vhcres*.txt files below via
# ResGen.exe. As of Stage D (2026-09), the .txt sources still carry 16 old/orphaned key
# names that the .resx files were deliberately renamed away from (dead/mistranslated
# fR-FR/ja/zh-cn/zh-tw keys - see commit bc13cc92 and vHC/VhcXTests/VbrLocalizationHelperTests.cs).
# Re-running this script will silently revert those renames. Update the .txt sources to
# match before running, or this undoes real translation-recovery work.
#cd C:\Users\cac89\Source\Repos\VeeamHub\veeam-healthcheck\vHC\HC_Reporting\Resources\Localization
$loc = "A:\source\veeam-healthcheck\vHC\HC_Reporting\Resources\Localization\"

& 'C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\Tools\Launch-VsDevShell.ps1'
start-sleep -Seconds 2

cd $loc
$files = gci -Name "vhcres*.txt"
foreach($f in $files){
 ResGen.exe $f
}

$content = Get-Content -LiteralPath "vhcres.txt"

$outFile = $loc + "VbrLocalizationHelper.cs"
echo $null | out-file $outFile
echo $null | out-file pubstrings.txt

"using System.Resources;`nnamespace VeeamHealthCheck.Resources.Localization`n{`n`nclass VbrLocalizationHelper`n{private static ResourceManager m4 = new(`"VeeamHealthCheck.Resources.Localization.vhcres`", typeof(VbrLocalizationHelper).Assembly);`n" | out-file $outFile

foreach($line in $content){
    if(!$line.StartsWith("#")){

        $split = $line.Split()
        if($split -ne $null){
        
            if($split[0] -ne "#"){
                $string = "public static string " + $split[0] + " = m4.GetString(`"" + $split[0] + "`");"
                
                #Write-Host($string)
                $string | Out-File -Append pubstrings.txt
                $string | out-file -Append $outFile
            }
        }
    }
}
"}}" | out-file -Append $outFile