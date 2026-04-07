#Requires -Version 5.1

function Get-VhciComplianceResults {
    <#
    .Synopsis
        Retrieves raw Security & Compliance Analyzer results for the given VBR version.
        v13+: calls Get-VBRSecurityComplianceAnalyzerResults cmdlet.
        v12:  calls [Veeam.Backup.DBManager.CDBManager]::Instance.BestPractices.GetAll().
        Callers are responsible for catching exceptions from this function.
    .Parameter VBRVersion
        Major VBR version integer.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [int] $VBRVersion
    )

    if ($VBRVersion -ge 13) {
        return Get-VBRSecurityComplianceAnalyzerResults
    } else {
        return [Veeam.Backup.DBManager.CDBManager]::Instance.BestPractices.GetAll()
    }
}
