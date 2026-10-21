#Requires -Version 5.1

function Register-VhciBrokenRepositoryReference {
    <#
    .Synopsis
        Records that one object references a repository that no longer exists, and logs it
        as a WARNING.
    .Description
        The collectors skip such an object (or emit it with the unresolved fields blank)
        and carry on. This keeps the skip visible: the WARNING goes to the collector log
        (never ERROR, so the CI log scan does not count a handled broken reference as a
        crash), and the entry is kept in $script:BrokenRepositoryReferences until
        Export-VhcBrokenRepositoryReferences writes _BrokenRepositoryReferences.csv, which
        the report shows in its Data Collection Summary. Entries are de-duplicated on
        object type + name + repository id.
    .Parameter ObjectType
        What was skipped, e.g. 'NAS backup job', 'Backup'.
    .Parameter ObjectName
        The object's name as Veeam knows it. Blank is recorded as '(unnamed)'.
    .Parameter RepositoryId
        The repository id from the "Unable to find repository id" message.
    .Parameter Collector
        The collector that hit it, e.g. 'NAS', 'Jobs'.
    .Parameter Consequence
        One sentence on what the report is missing because of it. Appended to the WARNING.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string]$ObjectType,
        [Parameter(Mandatory)] [AllowEmptyString()] [string]$ObjectName,
        [Parameter(Mandatory)] [string]$RepositoryId,
        [Parameter(Mandatory)] [string]$Collector,
        [Parameter(Mandatory = $false)] [string]$Consequence = 'It was skipped; everything else was collected.'
    )

    $registry = Get-Variable -Name BrokenRepositoryReferences -Scope Script -ErrorAction SilentlyContinue
    if ($null -eq $registry -or $null -eq $registry.Value) {
        $script:BrokenRepositoryReferences = [System.Collections.Generic.List[PSCustomObject]]::new()
    }

    $name = if ([string]::IsNullOrWhiteSpace($ObjectName)) { '(unnamed)' } else { $ObjectName }

    $known = $false
    foreach ($entry in $script:BrokenRepositoryReferences) {
        if ($entry.ObjectType -eq $ObjectType -and $entry.ObjectName -eq $name -and $entry.RepositoryId -eq $RepositoryId) {
            $known = $true
            break
        }
    }
    if (-not $known) {
        $script:BrokenRepositoryReferences.Add([PSCustomObject][ordered]@{
            ObjectType   = $ObjectType
            ObjectName   = $name
            RepositoryId = $RepositoryId
            Collector    = $Collector
        })
    }

    Write-LogFile "$ObjectType '$name' references repository $RepositoryId, which no longer exists. $Consequence" -LogLevel "WARNING"
}
