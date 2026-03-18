#Requires -Version 5.1

function Get-VhcModuleErrors {
    <#
    .Synopsis
        Returns the module-scoped error registry populated by public collector functions.
        This accessor is required because $script: refers to the *current* script file's scope.
        Get-VBRConfig.ps1 cannot read $script:ModuleErrors directly -- it would read its own
        (empty) script scope rather than the module's scope. Calling this exported function
        executes in module scope, so $script:ModuleErrors resolves correctly.
        Initialised by Initialize-VhcModule. See ADR 0007.
    #>
    [CmdletBinding()]
    param()

    return $script:ModuleErrors
}
