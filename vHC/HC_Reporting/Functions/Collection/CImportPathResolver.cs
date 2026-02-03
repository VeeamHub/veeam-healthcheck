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
    /// Resolves and validates import paths for CSV data files.
    /// Handles both flat structure (CSVs directly in folder) and nested structure (servername/timestamp subdirectories).
    /// </summary>
    public static class CImportPathResolver
    {
        private static readonly CLogger Log = CGlobals.Logger;
        private static readonly string LogPrefix = "[ImportPathResolver]\t";

        /// <summary>
        /// Critical VBR CSV files that should be present for a valid import.
        /// </summary>
        private static readonly string[] CriticalVbrFiles = new[]
        {
            "_Jobs.csv",
            "Proxies.csv",
            "Repositories.csv",
            "Servers.csv",
            "vbrinfo.csv"
        };

        /// <summary>
        /// Critical VB365 CSV files that should be present for a valid import.
        /// </summary>
        private static readonly string[] CriticalVb365Files = new[]
        {
            "Organizations.csv",
            "Proxies.csv",
            "Repositories.csv"
        };

        /// <summary>
        /// Find the directory containing CSV files within the provided base path.
        /// Handles both flat structure and nested servername/timestamp structure.
        /// </summary>
        /// <param name="basePath">The base path to search from.</param>
        /// <returns>Full path to directory containing CSV files, or null if not found.</returns>
        public static string FindCsvDirectory(string basePath)
        {
            if (string.IsNullOrEmpty(basePath))
            {
                Log.Error($"{LogPrefix}Import path is null or empty.");
                return null;
            }

            // Normalize path separators for cross-platform compatibility
            basePath = NormalizePath(basePath);

            if (!Directory.Exists(basePath))
            {
                Log.Error($"{LogPrefix}Import path does not exist: {basePath}");
                return null;
            }

            Log.Info($"{LogPrefix}Searching for CSV files in: {basePath}");

            // Strategy 1: Check if CSVs are directly in the provided path
            var directCsvs = GetCsvFilesInDirectory(basePath);
            if (HasCriticalFiles(directCsvs))
            {
                Log.Info($"{LogPrefix}Found CSV files directly in: {basePath}");
                return basePath;
            }

            // Strategy 2: Check for VBR or VB365 subdirectory structure
            var vbrSubdir = Path.Combine(basePath, "VBR");
            var vb365Subdir = Path.Combine(basePath, "VB365");

            string foundPath = null;

            if (Directory.Exists(vbrSubdir))
            {
                foundPath = FindCsvDirectoryInProductFolder(vbrSubdir);
                if (foundPath != null)
                {
                    Log.Info($"{LogPrefix}Found VBR CSV directory: {foundPath}");
                    CGlobals.IsVbr = true;
                    return foundPath;
                }
            }

            if (Directory.Exists(vb365Subdir))
            {
                foundPath = FindCsvDirectoryInProductFolder(vb365Subdir);
                if (foundPath != null)
                {
                    Log.Info($"{LogPrefix}Found VB365 CSV directory: {foundPath}");
                    CGlobals.IsVb365 = true;
                    return foundPath;
                }
            }

            // Strategy 3: Check for Original/VBR or Original/VB365 structure
            var originalSubdir = Path.Combine(basePath, "Original");
            if (Directory.Exists(originalSubdir))
            {
                vbrSubdir = Path.Combine(originalSubdir, "VBR");
                vb365Subdir = Path.Combine(originalSubdir, "VB365");

                if (Directory.Exists(vbrSubdir))
                {
                    foundPath = FindCsvDirectoryInProductFolder(vbrSubdir);
                    if (foundPath != null)
                    {
                        Log.Info($"{LogPrefix}Found VBR CSV directory in Original: {foundPath}");
                        CGlobals.IsVbr = true;
                        return foundPath;
                    }
                }

                if (Directory.Exists(vb365Subdir))
                {
                    foundPath = FindCsvDirectoryInProductFolder(vb365Subdir);
                    if (foundPath != null)
                    {
                        Log.Info($"{LogPrefix}Found VB365 CSV directory in Original: {foundPath}");
                        CGlobals.IsVb365 = true;
                        return foundPath;
                    }
                }
            }

            // Strategy 4: Recursive search for any directory with critical CSV files
            foundPath = FindCsvDirectoryRecursive(basePath);
            if (foundPath != null)
            {
                Log.Info($"{LogPrefix}Found CSV directory via recursive search: {foundPath}");
                return foundPath;
            }

            Log.Warning($"{LogPrefix}No directory with critical CSV files found under: {basePath}");
            return null;
        }

        /// <summary>
        /// Find CSV directory within a product folder (VBR or VB365).
        /// Handles servername/timestamp subdirectory structure.
        /// </summary>
        private static string FindCsvDirectoryInProductFolder(string productPath)
        {
            // First check if CSVs are directly in the product folder
            var directCsvs = GetCsvFilesInDirectory(productPath);
            if (HasCriticalFiles(directCsvs))
            {
                return productPath;
            }

            // Look for servername/timestamp structure
            var candidates = new List<(string Path, DateTime Timestamp)>();

            try
            {
                foreach (var serverDir in Directory.GetDirectories(productPath))
                {
                    foreach (var timestampDir in Directory.GetDirectories(serverDir))
                    {
                        var csvFiles = GetCsvFilesInDirectory(timestampDir);
                        if (HasCriticalFiles(csvFiles))
                        {
                            var (earliest, latest) = ExtractTimestamps(timestampDir);
                            candidates.Add((timestampDir, latest));
                        }
                    }

                    // Also check if CSVs are directly in server directory (no timestamp subfolder)
                    var serverCsvs = GetCsvFilesInDirectory(serverDir);
                    if (HasCriticalFiles(serverCsvs))
                    {
                        var (earliest, latest) = ExtractTimestamps(serverDir);
                        candidates.Add((serverDir, latest));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"{LogPrefix}Error searching product folder {productPath}: {ex.Message}");
            }

            // Return the most recent candidate
            if (candidates.Count > 0)
            {
                var best = candidates.OrderByDescending(c => c.Timestamp).First();
                return best.Path;
            }

            return null;
        }

        /// <summary>
        /// Recursively search for a directory containing critical CSV files.
        /// </summary>
        private static string FindCsvDirectoryRecursive(string basePath, int maxDepth = 5)
        {
            if (maxDepth <= 0)
                return null;

            var candidates = new List<(string Path, DateTime Timestamp)>();

            try
            {
                var csvFiles = GetCsvFilesInDirectory(basePath);
                if (HasCriticalFiles(csvFiles))
                {
                    var (earliest, latest) = ExtractTimestamps(basePath);
                    candidates.Add((basePath, latest));
                }

                foreach (var subDir in Directory.GetDirectories(basePath))
                {
                    var result = FindCsvDirectoryRecursive(subDir, maxDepth - 1);
                    if (result != null)
                    {
                        var (earliest, latest) = ExtractTimestamps(result);
                        candidates.Add((result, latest));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
            catch (Exception ex)
            {
                Log.Warning($"{LogPrefix}Error during recursive search in {basePath}: {ex.Message}");
            }

            // Return the most recent candidate
            if (candidates.Count > 0)
            {
                return candidates.OrderByDescending(c => c.Timestamp).First().Path;
            }

            return null;
        }

        /// <summary>
        /// Get list of CSV file names in a directory.
        /// </summary>
        private static List<string> GetCsvFilesInDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    return new List<string>();

                return Directory.GetFiles(path, "*.csv")
                    .Select(f => Path.GetFileName(f))
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Warning($"{LogPrefix}Error reading directory {path}: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Check if the list of CSV files contains critical files for either VBR or VB365.
        /// Handles both exact matches and servername-prefixed files (e.g., "localhost_Proxies.csv").
        /// </summary>
        private static bool HasCriticalFiles(List<string> csvFiles)
        {
            if (csvFiles == null || csvFiles.Count == 0)
                return false;

            // Check for VBR critical files (at least 3 of 5)
            // Match both exact name and {servername}_{filename} pattern
            int vbrMatches = CriticalVbrFiles.Count(f =>
                csvFiles.Any(c =>
                    c.Equals(f, StringComparison.OrdinalIgnoreCase) ||
                    c.EndsWith("_" + f, StringComparison.OrdinalIgnoreCase)));

            if (vbrMatches >= 3)
                return true;

            // Check for VB365 critical files (at least 2 of 3)
            int vb365Matches = CriticalVb365Files.Count(f =>
                csvFiles.Any(c =>
                    c.Equals(f, StringComparison.OrdinalIgnoreCase) ||
                    c.EndsWith("_" + f, StringComparison.OrdinalIgnoreCase)));

            return vb365Matches >= 2;
        }

        /// <summary>
        /// Validate required CSV files exist in the discovered directory.
        /// </summary>
        /// <param name="csvPath">Path to directory containing CSV files.</param>
        /// <returns>ValidationResult with list of missing critical files.</returns>
        public static ImportValidationResult ValidateCsvFiles(string csvPath)
        {
            var result = new ImportValidationResult
            {
                IsValid = false,
                CsvDirectory = csvPath,
                MissingCriticalFiles = new List<string>(),
                MissingOptionalFiles = new List<string>(),
                PresentFiles = new List<string>()
            };

            if (string.IsNullOrEmpty(csvPath) || !Directory.Exists(csvPath))
            {
                result.ErrorMessage = "CSV directory path is invalid or does not exist.";
                return result;
            }

            var csvFiles = GetCsvFilesInDirectory(csvPath);
            Log.Info($"{LogPrefix}Found {csvFiles.Count} CSV files in: {csvPath}");

            // Determine if this is VBR or VB365 based on files present
            bool isVbr = csvFiles.Any(f => f.Equals("_Jobs.csv", StringComparison.OrdinalIgnoreCase) ||
                                           f.Equals("vbrinfo.csv", StringComparison.OrdinalIgnoreCase));
            bool isVb365 = csvFiles.Any(f => f.Equals("Organizations.csv", StringComparison.OrdinalIgnoreCase));

            string[] criticalFiles = isVbr ? CriticalVbrFiles : CriticalVb365Files;

            foreach (var criticalFile in criticalFiles)
            {
                // Match both exact name and {servername}_{filename} pattern
                if (csvFiles.Any(f =>
                    f.Equals(criticalFile, StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith("_" + criticalFile, StringComparison.OrdinalIgnoreCase)))
                {
                    result.PresentFiles.Add(criticalFile);
                }
                else
                {
                    result.MissingCriticalFiles.Add(criticalFile);
                }
            }

            result.IsValid = result.MissingCriticalFiles.Count < criticalFiles.Length;
            result.ProductType = isVbr ? "VBR" : (isVb365 ? "VB365" : "Unknown");

            if (result.MissingCriticalFiles.Count > 0)
            {
                result.ErrorMessage = $"Missing {result.MissingCriticalFiles.Count} critical file(s): {string.Join(", ", result.MissingCriticalFiles)}";
                Log.Warning($"{LogPrefix}{result.ErrorMessage}");
            }
            else
            {
                Log.Info($"{LogPrefix}All critical CSV files present for {result.ProductType}.");
            }

            return result;
        }

        /// <summary>
        /// Extract timestamps from CSV file creation/modification times.
        /// </summary>
        /// <param name="csvPath">Path to directory containing CSV files.</param>
        /// <returns>Tuple of (earliest, latest) timestamps from the CSV files.</returns>
        public static (DateTime Earliest, DateTime Latest) ExtractTimestamps(string csvPath)
        {
            var earliest = DateTime.MaxValue;
            var latest = DateTime.MinValue;

            if (string.IsNullOrEmpty(csvPath) || !Directory.Exists(csvPath))
            {
                Log.Warning($"{LogPrefix}Cannot extract timestamps - invalid path: {csvPath}");
                return (DateTime.Now.AddDays(-7), DateTime.Now);
            }

            try
            {
                var csvFiles = Directory.GetFiles(csvPath, "*.csv");

                foreach (var file in csvFiles)
                {
                    var fileInfo = new FileInfo(file);

                    // Use the earlier of creation time and last write time
                    var fileTime = fileInfo.CreationTime < fileInfo.LastWriteTime
                        ? fileInfo.CreationTime
                        : fileInfo.LastWriteTime;

                    if (fileTime < earliest)
                        earliest = fileTime;
                    if (fileTime > latest)
                        latest = fileTime;
                }

                if (earliest == DateTime.MaxValue)
                {
                    // No files found, return defaults
                    return (DateTime.Now.AddDays(-7), DateTime.Now);
                }

                Log.Info($"{LogPrefix}CSV timestamps range from {earliest:yyyy-MM-dd HH:mm} to {latest:yyyy-MM-dd HH:mm}");
                return (earliest, latest);
            }
            catch (Exception ex)
            {
                Log.Warning($"{LogPrefix}Error extracting timestamps: {ex.Message}");
                return (DateTime.Now.AddDays(-7), DateTime.Now);
            }
        }

        /// <summary>
        /// Normalize path separators for cross-platform compatibility.
        /// </summary>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // Replace forward slashes with platform-specific separator
            return path.Replace('/', Path.DirectorySeparatorChar)
                       .Replace('\\', Path.DirectorySeparatorChar);
        }
    }

    /// <summary>
    /// Result of import path validation.
    /// </summary>
    public class ImportValidationResult
    {
        /// <summary>
        /// Gets or sets whether the import path is valid (has enough critical files).
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the resolved CSV directory path.
        /// </summary>
        public string CsvDirectory { get; set; }

        /// <summary>
        /// Gets or sets the detected product type (VBR, VB365, or Unknown).
        /// </summary>
        public string ProductType { get; set; }

        /// <summary>
        /// Gets or sets the list of missing critical files.
        /// </summary>
        public List<string> MissingCriticalFiles { get; set; }

        /// <summary>
        /// Gets or sets the list of missing optional files.
        /// </summary>
        public List<string> MissingOptionalFiles { get; set; }

        /// <summary>
        /// Gets or sets the list of present files.
        /// </summary>
        public List<string> PresentFiles { get; set; }

        /// <summary>
        /// Gets or sets any error message.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
