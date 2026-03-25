#Requires -Version 5.1

function Export-VhciCsv {
    <#
    .Synopsis
        Exports pipeline objects to a CSV file under the module's configured report path.
        Initialize-VhcModule must be called before this function.
    .Parameter FileName
        Suffix portion of the output filename (e.g. '_Jobs.csv'). The full path is
        constructed as: <ReportPath>\<VBRServer><FileName>
    .Parameter InputObject
        Objects to export. Accepts pipeline input.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [string] $FileName,
        [Parameter(ValueFromPipeline)] $InputObject
    )
    begin {
        # Validate module state on first call, not per-object
        if (-not $script:ReportPath -or -not $script:VBRServer) {
            throw "Initialize-VhcModule must be called before Export-VhciCsv"
        }
        $file = Join-Path $script:ReportPath ($script:VBRServer + $FileName)
        Write-LogFile "Exporting data to file: $file"
        $allObjects = [System.Collections.Generic.List[object]]::new()
    }
    process {
        if ($null -ne $InputObject) {
            $allObjects.Add($InputObject)
        }
    }
    end {
        if ($allObjects.Count -eq 0) {
            Write-LogFile "No records to export for '$file', skipping." -LogLevel "WARNING"
            return
        }
        try {
            $allObjects | Export-Csv -Path $file -NoTypeInformation -ErrorAction Stop
        } catch {
            Write-LogFile "Export-VhciCsv failed writing '$file': $($_.Exception.Message)" -LogLevel "ERROR"
            throw
        }
    }
}
