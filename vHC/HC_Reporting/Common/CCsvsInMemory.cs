using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;

namespace VeeamHealthCheck.Shared
{
    public static class CCsvsInMemory
    {
        private static readonly Dictionary<string, List<Dictionary<string, string>>> csvData
            = new Dictionary<string, List<Dictionary<string, string>>>();

        private static readonly object @lock = new object();

        public static bool LoadCsv(string filePath)
        {
            lock (@lock)
            {
                if (csvData.ContainsKey(filePath))
                {
                    return true;
                }

                try
                {
                    if (!File.Exists(filePath))
                    {
                        Console.WriteLine($"Warning: CSV file {filePath} not found.");
                        return false;
                    }

                    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        MissingFieldFound = null,
                        HeaderValidated = null,
                        BadDataFound = null,
                    };

                    var data = new List<Dictionary<string, string>>();

                    using var reader = new StreamReader(filePath);
                    using var csv = new CsvReader(reader, config);

                    if (!csv.Read())
                    {
                        csvData[filePath] = data;
                        Console.WriteLine($"Warning: CSV file {filePath} is empty. Loaded 0 rows.");
                        return true;
                    }
                    csv.ReadHeader();
                    var headers = csv.HeaderRecord.ToArray();

                    while (csv.Read())
                    {
                        var row = new Dictionary<string, string>();
                        for (int j = 0; j < headers.Length; j++)
                        {
                            row[headers[j]] = csv.GetField(j) ?? string.Empty;
                        }
                        data.Add(row);
                    }

                    csvData[filePath] = data;
                    Console.WriteLine($"Loaded CSV: {filePath} with {data.Count} rows.");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading CSV {filePath}: {ex.Message}");
                    return false;
                }
            }
        }

        public static List<Dictionary<string, string>> GetCsvData(string filePath)
        {
            lock (@lock)
            {
                if (csvData.TryGetValue(filePath, out var data))
                {
                    return data;
                }

                if (LoadCsv(filePath))
                {
                    return csvData[filePath];
                }

                return null;
            }
        }

        public static void Clear()
        {
            lock (@lock)
            {
                csvData.Clear();
            }
        }
    }
}