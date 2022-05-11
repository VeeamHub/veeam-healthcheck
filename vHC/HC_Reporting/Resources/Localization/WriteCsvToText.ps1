$content = Get-Content .\test.csv
$output = "vb365_resources.txt"
foreach($c in $content){
    $c = $c.Replace('"','')
    $c = $c.Split(",")
    $line = $c[0] + " = " + $c[1]
    $line | Out-File $output -Append
    Write-Host($line)





}