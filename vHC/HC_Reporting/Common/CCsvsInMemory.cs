using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VeeamHealthCheck.Shared
{
    public static class CCsvsInMemory
    {
        private static readonly Dictionary<string, List<Dictionary<string, string>>> _csvData
            = new Dictionary<string, List<Dictionary<string, string>>>();

        private static readonly object _lock = new object();

        public static bool LoadCsv(string filePath)
        {
            lock (_lock)
            {
                if (_csvData.ContainsKey(filePath))
                {
                    return true;
                }

                try
                {
                    var lines = File.ReadAllLines(filePath);
                    if (lines.Length == 0)
                    {
                        Console.WriteLine($"Warning: CSV file {filePath} is empty.");
                        return false;
                    }

                    var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
                    var data = new List<Dictionary<string, string>>();

                    for (int i = 1; i < lines.Length; i++)
                    {
                        var values = lines[i].Split(',').Select(v => v.Trim()).ToArray();
                        if (values.Length != headers.Length)
                        {
                            Console.WriteLine($"Warning: Row {i + 1} in {filePath} has mismatched columns. Skipping.");
                            continue;
                        }

                        var row = new Dictionary<string, string>();
                        for (int j = 0; j < headers.Length; j++)
                        {
                            row[headers[j]] = values[j];
                        }
                        data.Add(row);
                    }

                    _csvData[filePath] = data;
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
            lock (_lock)
            {
                if (_csvData.TryGetValue(filePath, out var data))
                {
                    return data;
                }

                if (LoadCsv(filePath))
                {
                    return _csvData[filePath];
                }

                return null;
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _csvData.Clear();
            }
        }
    }
}