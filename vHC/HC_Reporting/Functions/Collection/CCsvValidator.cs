// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Collection
{
    /// <summary>
    /// Validates the presence and accessibility of required CSV files.
    /// </summary>
    public class CCsvValidator
    {
        private readonly CLogger _log = CGlobals.Logger;
        private readonly string _logPrefix = "[CsvValidator]\t";
        private readonly string _csvDirectory;

        /// <summary>
        /// Defines expected CSV files with their severity levels.
        /// Critical = Report cannot be generated without this file.
        /// Warning = Report quality is reduced without this file.
        /// Info = Optional data that provides additional context.
        /// </summary>
        private static readonly Dictionary<string, CsvValidationSeverity> ExpectedVbrFiles = new()
        {
            // Critical files - core infrastructure data
            { "Proxies", CsvValidationSeverity.Critical },
            { "Repositories", CsvValidationSeverity.Critical },
            { "Servers", CsvValidationSeverity.Critical },
            { "vbrinfo", CsvValidationSeverity.Critical },
            { "_Jobs", CsvValidationSeverity.Critical },
            
            // Warning files - important but report can work without them
            { "SOBRs", CsvValidationSeverity.Warning },
            { "SOBRExtents", CsvValidationSeverity.Warning },
            { "HvProxy", CsvValidationSeverity.Warning },
            { "NasProxy", CsvValidationSeverity.Warning },
            { "CdpProxy", CsvValidationSeverity.Warning },
            { "LicInfo", CsvValidationSeverity.Warning },
            { "configBackup", CsvValidationSeverity.Warning },
            { "regkeys", CsvValidationSeverity.Warning },
            { "SecurityCompliance", CsvValidationSeverity.Warning },
            { "AllServersRequirementsComparison", CsvValidationSeverity.Warning },
            { "OptimizedConfiguration", CsvValidationSeverity.Warning },            
            { "SuboptimalConfiguration", CsvValidationSeverity.Warning },

            // Info files - nice to have but not essential
            { "WanAcc", CsvValidationSeverity.Info },
            { "capTier", CsvValidationSeverity.Info },
            { "trafficRules", CsvValidationSeverity.Info },
            { "waits", CsvValidationSeverity.Info },
            { "ViProtected", CsvValidationSeverity.Info },
            { "ViUnprotected", CsvValidationSeverity.Info },
            { "PhysProtected", CsvValidationSeverity.Info },
            { "PhysNotProtected", CsvValidationSeverity.Info },
            { "HvProtected", CsvValidationSeverity.Info },
            { "HvUnprotected", CsvValidationSeverity.Info },
            { "malware_settings", CsvValidationSeverity.Info },
            { "malware_infectedobject", CsvValidationSeverity.Info },
            { "malware_events", CsvValidationSeverity.Info },
            { "malware_exclusions", CsvValidationSeverity.Info },
            { "NasFileData", CsvValidationSeverity.Info },
            { "NasSharesize", CsvValidationSeverity.Info },
            { "NasObjectSourceStorageSize", CsvValidationSeverity.Info }
        };

        /// <summary>
        /// Initializes a new instance of the CCsvValidator class.
        /// </summary>
        /// <param name="csvDirectory">The directory containing CSV files to validate.</param>
        public CCsvValidator(string csvDirectory)
        {
            _csvDirectory = csvDirectory ?? throw new ArgumentNullException(nameof(csvDirectory));
        }

        /// <summary>
        /// Validates all expected VBR CSV files and returns the results.
        /// </summary>
        /// <returns>A list of validation results for all expected files.</returns>
        public List<CsvValidationResult> ValidateVbrCsvFiles()
        {
            var results = new List<CsvValidationResult>();

            _log.Info($"{_logPrefix}Starting CSV validation for VBR files in: {_csvDirectory}");

            foreach (var expectedFile in ExpectedVbrFiles)
            {
                var result = ValidateSingleFile(expectedFile.Key, expectedFile.Value);
                results.Add(result);

                // Log based on severity
                if (!result.IsPresent)
                {
                    switch (result.Severity)
                    {
                        case CsvValidationSeverity.Critical:
                            _log.Error($"{_logPrefix}CRITICAL: {result.Message}");
                            break;
                        case CsvValidationSeverity.Warning:
                            _log.Warning($"{_logPrefix}WARNING: {result.Message}");
                            break;
                        case CsvValidationSeverity.Info:
                            _log.Info($"{_logPrefix}INFO: {result.Message}");
                            break;
                    }
                }
                else
                {
                    _log.Debug($"{_logPrefix}{result.Message}");
                }
            }

            LogValidationSummary(results);

            return results;
        }

        /// <summary>
        /// Validates a single CSV file.
        /// </summary>
        /// <param name="fileName">The file name without extension.</param>
        /// <param name="severity">The severity level for this file.</param>
        /// <returns>The validation result.</returns>
       public CsvValidationResult ValidateSingleFile(string fileName, CsvValidationSeverity severity)
{
        try
        {
        // Search recursively, allow host prefixes like localhost_*.csv
        var matches = Directory.GetFiles(_csvDirectory, $"*{fileName}*.csv", SearchOption.AllDirectories);

        // If you're validating "Jobs", also accept legacy "_Jobs"
        if (matches.Length == 0 && fileName == "_Jobs")
        {
            matches = Directory.GetFiles(_csvDirectory, $"*Jobs*.csv", SearchOption.AllDirectories);
        }

        if (matches.Length == 0)
        {
            // Keep expected path for message readability
            string expectedPath = Path.Combine(_csvDirectory, fileName + ".csv");
            return CsvValidationResult.Missing(fileName, expectedPath, severity);
        }

        // Prefer the most direct match if multiple exist
        string filePath = matches
            .OrderBy(p => Path.GetFileName(p).Length) // usually "localhost_X.csv" is shortest/best
            .First();

        int lineCount = File.ReadLines(filePath).Count();
        int recordCount = Math.Max(0, lineCount - 1);

        return CsvValidationResult.Present(fileName, filePath, recordCount);
        }
        catch (Exception ex)
        {
            _log.Error($"{_logPrefix}Error locating/reading file '{fileName}': {ex.Message}");
            return new CsvValidationResult
            {
                FileName = fileName,
                FilePath = Path.Combine(_csvDirectory, fileName + ".csv"),
                IsPresent = false,
                Severity = severity,
                RecordCount = 0,
                Message = $"Error reading '{fileName}': {ex.Message}",
                ValidationTime = DateTime.Now
            };
        }
}

        /// <summary>
        /// Logs a summary of all validation results.
        /// </summary>
        /// <param name="results">The validation results to summarize.</param>
        private void LogValidationSummary(List<CsvValidationResult> results)
        {
            int presentCount = results.Count(r => r.IsPresent);
            int missingCount = results.Count(r => !r.IsPresent);
            int criticalMissing = results.Count(r => !r.IsPresent && r.Severity == CsvValidationSeverity.Critical);
            int warningMissing = results.Count(r => !r.IsPresent && r.Severity == CsvValidationSeverity.Warning);
            int infoMissing = results.Count(r => !r.IsPresent && r.Severity == CsvValidationSeverity.Info);
            int totalRecords = results.Where(r => r.IsPresent).Sum(r => r.RecordCount);

            _log.Info($"{_logPrefix}=== CSV Validation Summary ===");
            _log.Info($"{_logPrefix}Files present: {presentCount}/{results.Count}");
            _log.Info($"{_logPrefix}Total records loaded: {totalRecords}");
            
            if (missingCount > 0)
            {
                _log.Info($"{_logPrefix}Missing files: {missingCount} (Critical: {criticalMissing}, Warning: {warningMissing}, Info: {infoMissing})");
            }

            if (criticalMissing > 0)
            {
                _log.Warning($"{_logPrefix}WARNING: {criticalMissing} critical file(s) missing - report quality may be significantly impacted.");
            }
        }

        /// <summary>
        /// Gets a summary message suitable for display in reports.
        /// </summary>
        /// <param name="results">The validation results.</param>
        /// <returns>A formatted summary string.</returns>
        public static string GetReportSummary(List<CsvValidationResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return "No CSV validation data available.";
            }

            int presentCount = results.Count(r => r.IsPresent);
            int totalCount = results.Count;
            int criticalMissing = results.Count(r => !r.IsPresent && r.Severity == CsvValidationSeverity.Critical);
            int warningMissing = results.Count(r => !r.IsPresent && r.Severity == CsvValidationSeverity.Warning);

            if (criticalMissing > 0)
            {
                return $"Data Collection: {presentCount}/{totalCount} files loaded. {criticalMissing} critical file(s) missing - some report sections may be incomplete.";
            }
            else if (warningMissing > 0)
            {
                return $"Data Collection: {presentCount}/{totalCount} files loaded. {warningMissing} optional file(s) missing.";
            }
            else
            {
                return $"Data Collection: {presentCount}/{totalCount} files loaded successfully.";
            }
        }

        /// <summary>
        /// Gets the list of missing files with their severity levels.
        /// </summary>
        /// <param name="results">The validation results.</param>
        /// <returns>A list of missing file details.</returns>
        public static List<string> GetMissingFilesList(List<CsvValidationResult> results)
        {
            return results
                .Where(r => !r.IsPresent)
                .OrderByDescending(r => r.Severity)
                .Select(r => $"[{r.Severity}] {r.FileName}")
                .ToList();
        }
    }
}
