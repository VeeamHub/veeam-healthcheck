$string = "Organization","Name","Description","Job Type","Scope Type","Selected Items","Excluded Items","Repository","Bound Proxy","Enabled?","Schedule","Related Job"













foreach($s in $string){
    $line = 's += TableHeader("' + $s + '","' + $s + '");'
    write-host($line)
}