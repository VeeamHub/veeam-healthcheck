$list = @("Sobr","SobrExt", "Repo", "JobCon", "TaskCon", "JobSessSumm", "JobInfo")

foreach($i in $list){

    write-host("public string Add" +$i + "Table()`n{`nreturn null;`n}")

}