// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;

namespace VeeamHealthCheck.Shared
{
    /// <summary>
    /// Severity levels for CSV validation results.
    /// </summary>
    public enum CsvValidationSeverity
    {
        /// <summary>
        /// Informational - file is optional and missing won't affect core functionality.
        /// </summary>
        Info,

        /// <summary>
        /// Warning - file provides useful data but report can still be generated without it.
        /// </summary>
        Warning,

        /// <summary>
        /// Critical - file is essential and missing will significantly impact report quality.
        /// </summary>
        Critical
    }

    /// <summary>
    /// Represents the validation result for a single CSV file.
    /// </summary>
    public class CsvValidationResult
    {
        /// <summary>
        /// Gets or sets the name of the CSV file (without path or extension).
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the file was found and readable.
        /// </summary>
        public bool IsPresent { get; set; }

        /// <summary>
        /// Gets or sets the severity level indicating how important this file is.
        /// </summary>
        public CsvValidationSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the full file path that was checked.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets the number of records found in the file (0 if not present or empty).
        /// </summary>
        public int RecordCount { get; set; }

        /// <summary>
        /// Gets or sets an optional message providing additional context.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when validation was performed.
        /// </summary>
        public DateTime ValidationTime { get; set; }

        /// <summary>
        /// Creates a new CsvValidationResult for a missing file.
        /// </summary>
        /// <param name="fileName">The name of the missing file.</param>
        /// <param name="filePath">The full path that was checked.</param>
        /// <param name="severity">The severity level for this missing file.</param>
        /// <returns>A CsvValidationResult indicating a missing file.</returns>
        public static CsvValidationResult Missing(string fileName, string filePath, CsvValidationSeverity severity)
        {
            return new CsvValidationResult
            {
                FileName = fileName,
                FilePath = filePath,
                IsPresent = false,
                Severity = severity,
                RecordCount = 0,
                Message = $"CSV file '{fileName}' not found at expected location.",
                ValidationTime = DateTime.Now
            };
        }

        /// <summary>
        /// Creates a new CsvValidationResult for a present file.
        /// </summary>
        /// <param name="fileName">The name of the file.</param>
        /// <param name="filePath">The full path to the file.</param>
        /// <param name="recordCount">The number of records in the file.</param>
        /// <returns>A CsvValidationResult indicating a present file.</returns>
        public static CsvValidationResult Present(string fileName, string filePath, int recordCount)
        {
            return new CsvValidationResult
            {
                FileName = fileName,
                FilePath = filePath,
                IsPresent = true,
                Severity = CsvValidationSeverity.Info,
                RecordCount = recordCount,
                Message = recordCount > 0 
                    ? $"CSV file '{fileName}' loaded with {recordCount} records."
                    : $"CSV file '{fileName}' found but contains no records.",
                ValidationTime = DateTime.Now
            };
        }

        /// <summary>
        /// Returns a string representation of the validation result.
        /// </summary>
        public override string ToString()
        {
            string status = IsPresent ? "Present" : "Missing";
            return $"[{Severity}] {FileName}: {status} ({RecordCount} records) - {Message}";
        }
    }
}
