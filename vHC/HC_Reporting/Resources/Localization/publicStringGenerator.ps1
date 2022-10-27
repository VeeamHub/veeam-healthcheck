$list = @("License Status","License Expiry","Support Expiry","License Type","Licensed To","License Contact","Licensed For","Licenses Used","Global Folder Exclusions","Global Ret. Exclusions","Log Retention","Notification Enabled","Notifify On","Automatic Updates?")
$p = 0
foreach($i in $list){
    $new = $i.Replace('?', '')
    $new = $new.Replace(" ", '')
    $new = $new.Replace('-','')
    write-host ("[Index($p)]")
    write-host("public string $new{get;set;}")
    $p++
}