#Requires -Version 5.1

function Get-VhciMissingRepositoryId {
    <#
    .Synopsis
        Returns the repository id named by a "repository no longer exists" error, or $null
        when the message is about anything else.
    .Description
        Veeam raises "Unable to find repository id <guid>" when an object (a job, a backup,
        a restore point, a scale-out extent) still points at a repository that was deleted
        or is otherwise gone. That is a property of the customer's environment, not a
        failure of the collector, so callers use this to tell it apart from a real error
        and skip only the one object. Anything that does not match is a real error and
        must still propagate.
    .Parameter Message
        The exception message to inspect.
    .Outputs
        [string] the repository id, or $null.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Message
    )

    if ([string]::IsNullOrEmpty($Message)) { return $null }

    $match = [regex]::Match($Message, 'Unable to find repository id\s+\{?([0-9a-fA-F-]+)\}?', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($match.Success) { return $match.Groups[1].Value }

    return $null
}
