#Requires -Version 5.1

function Export-VhcBrokenRepositoryReferences {
    <#
    .Synopsis
        Writes _BrokenRepositoryReferences.csv: every job, backup or scale-out repository the
        collectors skipped because it references a repository that no longer exists.
    .Description
        Entries come from Register-VhciBrokenRepositoryReference, called by the collectors
        when Veeam reports "Unable to find repository id <guid>" for one object. Called once
        by Get-VBRConfig.ps1 after every collector has run. Writes nothing when no
        reference was broken (Export-VhciCsv skips zero-record exports), so an estate
        without this problem produces no file. The report reads the file into its Data
        Collection Summary.
        Columns: ObjectType, ObjectName, RepositoryId, Collector.
    #>
    [CmdletBinding()]
    param()

    $registry = Get-Variable -Name BrokenRepositoryReferences -Scope Script -ErrorAction SilentlyContinue
    if ($null -eq $registry -or $null -eq $registry.Value -or $registry.Value.Count -eq 0) {
        Write-LogFile "No object references a repository that no longer exists."
        return
    }

    try {
        Write-LogFile "$($registry.Value.Count) object(s) reference a repository that no longer exists. Listing them in the report."
        @($registry.Value) | Export-VhciCsv -FileName '_BrokenRepositoryReferences.csv'
    } catch {
        Write-LogFile "Could not write the list of objects that reference a missing repository: $($_.Exception.Message)" -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'BrokenRepositoryReferences' -ErrorMessage $_.Exception.Message
    }
}
