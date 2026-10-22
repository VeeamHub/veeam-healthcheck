#Requires -Version 5.1

function Invoke-VhciTolerantEnumeration {
    <#
    .Synopsis
        Runs a VBR enumeration cmdlet so that one object pointing at a repository that no
        longer exists does not discard every other object.
    .Description
        Several Veeam cmdlets resolve each object's repository while listing, and fail with
        "Unable to find repository id <guid>" when one of them is gone. The collectors run
        with $ErrorActionPreference = 'Stop', so before this helper that one object threw
        away the whole collector's output.

        The command runs with errors set to Continue and its output is streamed into a
        list, so:
          - when Veeam reports the bad object as a non-terminating error, every other
            object is still returned;
          - when Veeam aborts the listing with a terminating error, the objects it emitted
            before the abort are kept, and (when -PerObjectCommand and -ObjectNames are
            given) the remaining objects are read one at a time so only the bad ones are
            lost.
        Each object lost to a missing repository is recorded with
        Register-VhciBrokenRepositoryReference (a WARNING, plus the report finding).

        Any other error is a real failure and is rethrown unchanged, so a collector that
        genuinely crashes still fails exactly as before. Nothing is invented for the
        objects that could not be read.
    .Parameter Command
        Scriptblock that lists the objects, e.g. { Get-VBRUnstructuredBackupJob -ErrorAction Continue }.
    .Parameter ObjectType
        What the objects are, for the log and the report, e.g. 'NAS backup job'.
    .Parameter Collector
        The collector name recorded with each skipped object.
    .Parameter PerObjectCommand
        Optional scriptblock taking one name, returning that single object, e.g.
        { param($Name) Get-VBRUnstructuredBackupJob -Name $Name }. Only used when the
        listing was aborted.
    .Parameter ObjectNames
        Names of the objects expected from the listing (from job discovery). Names already
        returned by the aborted listing are not read again.
    .Outputs
        [object[]] the objects that could be read (possibly empty). Wrap the call in @().
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [scriptblock]$Command,
        [Parameter(Mandatory)] [string]$ObjectType,
        [Parameter(Mandatory)] [string]$Collector,
        [Parameter(Mandatory = $false)] [scriptblock]$PerObjectCommand,
        [Parameter(Mandatory = $false)] [string[]]$ObjectNames = @()
    )

    $items        = [System.Collections.Generic.List[object]]::new()
    $otherErrors  = [System.Collections.Generic.List[object]]::new()
    $skipped      = [System.Collections.Generic.List[object]]::new()
    $terminating  = $null

    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Command 2>&1 | ForEach-Object {
            if ($_ -is [System.Management.Automation.ErrorRecord]) {
                $repoId = Get-VhciMissingRepositoryId -Message $_.Exception.Message
                if ($repoId) {
                    $skipped.Add([PSCustomObject]@{ RepositoryId = $repoId; Target = $_.TargetObject })
                } else {
                    $otherErrors.Add($_)
                }
            } else {
                $items.Add($_)
            }
        }
    } catch {
        $terminating = $_
    } finally {
        $ErrorActionPreference = $previousPreference
    }

    # A real failure still fails the collector, exactly as it did before.
    if ($otherErrors.Count -gt 0) { throw $otherErrors[0] }

    $abortRepoId = $null
    if ($null -ne $terminating) {
        $abortRepoId = Get-VhciMissingRepositoryId -Message $terminating.Exception.Message
        if ($null -eq $abortRepoId) { throw $terminating }
    }

    # Non-terminating: Veeam skipped the object itself and kept going.
    foreach ($skip in $skipped) {
        $targetName = $null
        if ($null -ne $skip.Target) {
            try { $targetName = [string]$skip.Target.Name } catch { $targetName = $null }
        }
        if ([string]::IsNullOrWhiteSpace($targetName)) { $targetName = '(not named by Veeam)' }
        Register-VhciBrokenRepositoryReference -ObjectType $ObjectType -ObjectName $targetName -RepositoryId $skip.RepositoryId -Collector $Collector `
            -Consequence 'Veeam skipped it while listing; every other object was collected.'
    }

    # Terminating: Veeam aborted the listing at the first bad object.
    if ($null -ne $abortRepoId) {
        if ($null -ne $PerObjectCommand -and @($ObjectNames).Count -gt 0) {
            $have = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            foreach ($item in $items) {
                $itemName = $null
                try { $itemName = [string]$item.Name } catch { $itemName = $null }
                if (-not [string]::IsNullOrWhiteSpace($itemName)) { [void]$have.Add($itemName) }
            }

            $recovered = 0
            foreach ($objectName in @($ObjectNames)) {
                if ([string]::IsNullOrWhiteSpace($objectName) -or $have.Contains($objectName)) { continue }
                [void]$have.Add($objectName)
                try {
                    foreach ($one in @(& $PerObjectCommand $objectName)) {
                        if ($null -ne $one) { $items.Add($one); $recovered++ }
                    }
                } catch {
                    $oneRepoId = Get-VhciMissingRepositoryId -Message $_.Exception.Message
                    if ($oneRepoId) {
                        Register-VhciBrokenRepositoryReference -ObjectType $ObjectType -ObjectName $objectName -RepositoryId $oneRepoId -Collector $Collector `
                            -Consequence 'It was skipped; every other object was collected.'
                    } else {
                        Write-LogFile "$ObjectType '$objectName' could not be read on its own and was skipped: $($_.Exception.Message)" -LogLevel "WARNING"
                    }
                }
            }
            Write-LogFile "$ObjectType listing stopped at an object that references repository $abortRepoId, which no longer exists. Read the remaining objects one by one: $recovered recovered." -LogLevel "WARNING"
        } else {
            Register-VhciBrokenRepositoryReference -ObjectType $ObjectType -ObjectName '(first object Veeam could not list)' -RepositoryId $abortRepoId -Collector $Collector `
                -Consequence "The listing stopped there: $($items.Count) object(s) were read before it, and any after it could not be listed."
        }
    }

    return $items.ToArray()
}
