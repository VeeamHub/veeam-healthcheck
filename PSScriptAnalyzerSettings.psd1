# PSScriptAnalyzer settings, used by .github/workflows/psscriptanalyzer.yml (advisory, never fails a PR).
# Locally: Invoke-ScriptAnalyzer -Path <file> -Settings ./PSScriptAnalyzerSettings.psd1
#
# Every excluded rule carries a one-line reason. Remove an exclusion when that reason stops holding.
@{
    Severity     = @('Error', 'Warning', 'Information')

    ExcludeRules = @(
        # Collection scripts, build helpers and CI scripts print progress to the console by design; nothing captures that output.
        'PSAvoidUsingWriteHost'
    )
}
