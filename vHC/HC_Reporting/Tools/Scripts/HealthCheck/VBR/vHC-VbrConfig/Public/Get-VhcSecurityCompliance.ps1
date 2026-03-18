#Requires -Version 5.1

function Get-VhcSecurityCompliance {
    <#
    .Synopsis
        Triggers a Security & Compliance Analyzer scan and exports results.
        Polls for results every 3 seconds up to 45 seconds rather than sleeping a fixed interval.
        v12:  reads results via [Veeam.Backup.DBManager.CDBManager]::Instance.BestPractices.GetAll()
        v13+: reads results via Get-VBRSecurityComplianceAnalyzerResults cmdlet
        Rule names are resolved from $Config.SecurityComplianceRuleNames (VbrConfig.json).
        Unknown rule types are output with the raw Type string (not dropped) to preserve
        visibility of new compliance rules when the JSON mapping is stale.
        Gated on VBR v12+ (VBRVersion -gt 11).
        Exports _SecurityCompliance.csv.
        Source: Get-VBRConfig.ps1 lines 1677-1991.
    .Parameter VBRVersion
        Major VBR version integer. Function is a no-op for versions 11 and below.
    .Parameter Config
        Deserialized VbrConfig.json object. Must contain a SecurityComplianceRuleNames property.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [int]$VBRVersion = 0,

        [Parameter(Mandatory = $true)]
        [object]$Config
    )

    if ($VBRVersion -le 11) {
        Write-LogFile "VBR Version ($VBRVersion) does not support Security & Compliance - skipping"
        return
    }

    Write-LogFile "VBR Version ($VBRVersion) supports Security & Compliance - starting collection..."

    $StatusObj = @{
        'Ok'            = 'Passed'
        'Violation'     = 'Not Implemented'
        'UnableToCheck' = 'Unable to detect'
        'Suppressed'    = 'Suppressed'
    }

    try {
        # ---------------------------------------------------------------
        # Trigger a fresh scan
        # ---------------------------------------------------------------
        Write-LogFile "Starting Security & Compliance scan..."
        Write-LogFile "Calling Start-VBRSecurityComplianceAnalyzer..."

        try {
            Start-VBRSecurityComplianceAnalyzer -ErrorAction Stop `
                -WarningAction SilentlyContinue -InformationAction SilentlyContinue
            Write-LogFile "Start-VBRSecurityComplianceAnalyzer completed successfully"
        }
        catch {
            $errMsg  = if ($_.Exception.Message) { $_.Exception.Message.ToString() } else { "No error message" }
            $errType = if ($_.Exception)         { $_.Exception.GetType().FullName  } else { "Unknown" }
            Write-LogFile "Start-VBRSecurityComplianceAnalyzer failed: $errMsg" -LogLevel "ERROR"
            Write-LogFile "Exception Type: $errType" -LogLevel "ERROR"
            throw
        }

        # ---------------------------------------------------------------
        # Poll for results (max 45 s, every 3 s)
        # ---------------------------------------------------------------
        $maxWaitSeconds      = if ($Config.Thresholds.CompliancePollMaxSeconds)      { [int]$Config.Thresholds.CompliancePollMaxSeconds }      else { 45 }
        $pollIntervalSeconds = if ($Config.Thresholds.CompliancePollIntervalSeconds) { [int]$Config.Thresholds.CompliancePollIntervalSeconds } else { 3 }
        $elapsed           = 0
        $SecurityCompliances = $null
        Write-LogFile "Polling for scan results (max ${maxWaitSeconds}s, every ${pollIntervalSeconds}s)..."

        while ($elapsed -lt $maxWaitSeconds) {
            Start-Sleep -Seconds $pollIntervalSeconds
            $elapsed += $pollIntervalSeconds
            try {
                $SecurityCompliances = Get-VhciComplianceResults -VBRVersion $VBRVersion
                if ($SecurityCompliances -and $SecurityCompliances.Count -gt 0) {
                    Write-LogFile "Scan results ready after ${elapsed}s - retrieved $($SecurityCompliances.Count) compliance items"
                    break
                }
            }
            catch {
                Write-LogFile "Poll attempt at ${elapsed}s not ready yet, retrying..."
            }
        }

        # Final attempt if polling timed out
        if (-not $SecurityCompliances -or $SecurityCompliances.Count -eq 0) {
            Write-LogFile "Polling timed out after ${maxWaitSeconds}s. Final retrieval attempt..."
            try {
                $SecurityCompliances = Get-VhciComplianceResults -VBRVersion $VBRVersion
                Write-LogFile "Retrieved $($SecurityCompliances.Count) compliance items"
            }
            catch {
                $errMsg = if ($_.Exception.Message) { $_.Exception.Message.ToString() } else { "No error message" }
                Write-LogFile "Failed to retrieve compliance data: $errMsg" -LogLevel "ERROR"
                throw
            }
        }

        Write-LogFile "Security & Compliance scan completed."

        # ---------------------------------------------------------------
        # Map results to output rows
        # ---------------------------------------------------------------
        $OutObj = [System.Collections.ArrayList]::new()
        Write-LogFile "Processing $($SecurityCompliances.Count) compliance rules..."
        Write-LogFile "SecurityComplianceRuleNames has $($Config.SecurityComplianceRuleNames.PSObject.Properties.Count) entries"

        $unmappedTypes  = [System.Collections.Generic.List[string]]::new()
        $processedCount = 0
        $skippedCount   = 0
        $errorCount     = 0

        foreach ($SecurityCompliance in $SecurityCompliances) {
            try {
                $complianceType   = $null
                $complianceStatus = $null

                if ($SecurityCompliance.Type) {
                    $complianceType = $SecurityCompliance.Type.ToString()
                }
                else {
                    Write-LogFile "Warning: Compliance item has null Type - skipping" -LogLevel "WARNING"
                    $skippedCount++
                    continue
                }

                if ($SecurityCompliance.Status) {
                    $complianceStatus = $SecurityCompliance.Status.ToString()
                }
                else {
                    Write-LogFile "Warning: Compliance item '$complianceType' has null Status - skipping" -LogLevel "WARNING"
                    $skippedCount++
                    continue
                }

                # Resolve rule name - fall back to raw type string for unknown rules
                # so that new compliance checks remain visible even when VbrConfig.json is stale.
                $ruleName = $Config.SecurityComplianceRuleNames.$complianceType
                if (-not $ruleName) {
                    $unmappedTypes.Add($complianceType)
                    $ruleName = $complianceType
                }

                # Resolve status - fall back to raw status string for unknown values
                $statusDisplay = if ($StatusObj.ContainsKey($complianceStatus)) {
                    $StatusObj[$complianceStatus]
                }
                else {
                    Write-LogFile "Warning: Unknown compliance status '$complianceStatus' for type '$complianceType' - using raw value" -LogLevel "WARNING"
                    $complianceStatus
                }

                $inObj = [pscustomobject][ordered]@{
                    'Best Practice' = $ruleName
                    'Status'        = $statusDisplay
                }
                [void]$OutObj.Add($inObj)
                $processedCount++
            }
            catch {
                $errorCount++
                $errMsg  = if ($_.Exception.Message) { $_.Exception.Message.ToString() } else { "No error message" }
                $errType = if ($_.Exception)         { $_.Exception.GetType().FullName  } else { "Unknown" }
                Write-LogFile "Error processing compliance rule ($errorCount): $errMsg" -LogLevel "ERROR"
                Write-LogFile "Exception Type: $errType" -LogLevel "ERROR"
                if ($complianceType)   { Write-LogFile "Rule Type: $complianceType"     -LogLevel "ERROR" }
                if ($complianceStatus) { Write-LogFile "Rule Status: $complianceStatus" -LogLevel "ERROR" }
            }
        }

        Write-LogFile "Processed $processedCount compliance rules, skipped $skippedCount, errors $errorCount"
        Write-LogFile "OutObj count: $($OutObj.Count)"

        if ($unmappedTypes.Count -gt 0) {
            $validatedFor = $Config.SecurityComplianceRulesValidatedForVbrVersion
            $msg = "$($unmappedTypes.Count) compliance rule(s) have no label mapping in VbrConfig.json " +
                   "(mapping validated for VBR $validatedFor, running VBR $VBRVersion): " +
                   ($unmappedTypes -join ', ')
            Write-LogFile $msg -LogLevel "WARNING"
            Add-VhciModuleError -CollectorName 'SecurityCompliance' -ErrorMessage $msg
        }

        if ($OutObj.Count -gt 0) {
            try {
                Write-LogFile "Exporting $($OutObj.Count) compliance items to CSV..."
                $OutObj | Export-VhciCsv -FileName '_SecurityCompliance.csv'
                Write-LogFile "Security Compliance CSV export completed successfully"
            }
            catch {
                $errMsg     = if ($_.Exception.Message)    { $_.Exception.Message.ToString()   } else { "No error message" }
                $errType    = if ($_.Exception)            { $_.Exception.GetType().FullName    } else { "Unknown" }
                $stackTrace = if ($_.ScriptStackTrace)     { $_.ScriptStackTrace.ToString()     } else { "No stack trace" }
                Write-LogFile "Failed to export Security Compliance CSV: $errMsg" -LogLevel "ERROR"
                Write-LogFile "Exception Type: $errType"                          -LogLevel "ERROR"
                Write-LogFile "Stack Trace: $stackTrace"                          -LogLevel "ERROR"
            }
        }
        else {
            Write-LogFile "No compliance data to export - OutObj is empty" -LogLevel "WARNING"
        }
    }
    catch {
        $errMsg     = if ($_.Exception.Message) { $_.Exception.Message.ToString() } else { $_.ToString() }
        $errType    = if ($_.Exception)         { $_.Exception.GetType().FullName  } else { "Unknown" }
        $stackTrace = if ($_.ScriptStackTrace)  { $_.ScriptStackTrace.ToString()   } else { "No stack trace available" }
        Write-LogFile "Security & Compliance collection failed: $errMsg" -LogLevel "ERROR"
        Write-LogFile "Exception Type: $errType"                         -LogLevel "ERROR"
        Write-LogFile "Stack Trace: $stackTrace"                         -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'SecurityCompliance' -ErrorMessage $errMsg
    }
}
