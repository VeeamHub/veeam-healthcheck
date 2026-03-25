// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Collection.REST
{
    internal class CVbawsRestCollector
    {
        private readonly CLogger log = CGlobals.Logger;
        private readonly HttpClient _client;
        private readonly string _baseUrl;
        private string _token;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public CVbawsRestCollector(string host, int port, bool trustCert)
        {
            _baseUrl = $"https://{host}:{port}";
            var handler = new HttpClientHandler();
            if (trustCert)
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            }
            _client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public virtual bool Authenticate(string username, string password)
        {
            try
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "password"),
                    new KeyValuePair<string, string>("username", username),
                    new KeyValuePair<string, string>("password", password)
                });

                var response = _client.PostAsync($"{_baseUrl}/api/v1/token", content).GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    log.Error($"VBAWS authentication failed: {response.StatusCode}", false);
                    return false;
                }

                var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(json);
                _token = doc.RootElement.GetProperty("access_token").GetString();
                var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
                _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60); // refresh 60s early

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                log.Info("VBAWS authentication successful", false);
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"VBAWS authentication error: {ex.Message}", false);
                return false;
            }
        }

        private void EnsureValidToken()
        {
            if (DateTime.Now >= _tokenExpiry)
            {
                log.Warning("VBAWS token expired or near expiry - re-authentication required", false);
                throw new InvalidOperationException("VBAWS token expired. Re-authenticate before continuing.");
            }
        }

        public virtual string GetPolicies()
        {
            EnsureValidToken();
            return GetJson("/api/v1/virtualMachines/policies");
        }

        public virtual string GetSessions()
        {
            EnsureValidToken();
            return GetJson("/api/v1/sessions");
        }

        public virtual string GetRestorePoints()
        {
            EnsureValidToken();
            return GetJson("/api/v1/virtualMachines/restorePoints");
        }

        public virtual string GetRepositories()
        {
            EnsureValidToken();
            return GetJson("/api/v1/repositories");
        }

        private string GetJson(string endpoint)
        {
            try
            {
                var response = _client.GetAsync($"{_baseUrl}{endpoint}").GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    log.Error($"VBAWS API call failed: {endpoint} -> {response.StatusCode}", false);
                    return null;
                }
                return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                log.Error($"VBAWS API error on {endpoint}: {ex.Message}", false);
                return null;
            }
        }

        public void CollectAll(string outputDir)
        {
            log.Info("Starting VBAWS data collection...", false);

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var applianceId = CGlobals.VbawsHost;

            CollectEndpoint("Policies", GetPolicies, outputDir, "_VbawsPolicies.csv", applianceId);
            CollectEndpoint("Sessions", GetSessions, outputDir, "_VbawsSessions.csv", applianceId);
            CollectEndpoint("RestorePoints", GetRestorePoints, outputDir, "_VbawsRestorePoints.csv", applianceId);
            CollectEndpoint("Repositories", GetRepositories, outputDir, "_VbawsRepositories.csv", applianceId);

            log.Info("VBAWS data collection complete", false);
        }

        private void CollectEndpoint(string name, Func<string> getter, string outputDir, string fileName, string applianceId)
        {
            try
            {
                log.Info($"Collecting VBAWS {name}...", false);
                var json = getter();
                if (!string.IsNullOrEmpty(json))
                {
                    var csvPath = Path.Combine(outputDir, fileName);
                    JsonToCsv(json, csvPath, applianceId);
                    log.Info($"VBAWS {name} written to {csvPath}", false);
                }
                else
                {
                    log.Warning($"VBAWS {name} returned no data", false);
                }
            }
            catch (Exception ex)
            {
                log.Error($"VBAWS {name} collection failed: {ex.Message}", false);
            }
        }

        internal static void JsonToCsv(string json, string csvPath, string applianceId)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Handle both array responses and object-with-results responses
            JsonElement items;
            if (root.ValueKind == JsonValueKind.Array)
            {
                items = root;
            }
            else if (root.TryGetProperty("results", out var results))
            {
                items = results;
            }
            else if (root.TryGetProperty("data", out var data))
            {
                items = data;
            }
            else
            {
                // Single object - wrap as raw JSON row
                File.WriteAllText(csvPath, "ApplianceId,RawJson\n");
                File.AppendAllText(csvPath, $"\"{applianceId}\",\"{EscapeCsvField(root.GetRawText())}\"\n");
                return;
            }

            if (items.GetArrayLength() == 0)
            {
                File.WriteAllText(csvPath, "ApplianceId\n");
                return;
            }

            // Extract headers from first item
            var headers = new List<string> { "ApplianceId" };
            foreach (var prop in items[0].EnumerateObject())
            {
                headers.Add(prop.Name);
            }

            using var writer = new StreamWriter(csvPath);
            writer.WriteLine(string.Join(",", headers.ConvertAll(h => $"\"{h}\"")));

            foreach (var item in items.EnumerateArray())
            {
                var values = new List<string> { $"\"{applianceId}\"" };
                foreach (var prop in item.EnumerateObject())
                {
                    var val = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString()
                        : prop.Value.GetRawText();
                    values.Add($"\"{EscapeCsvField(val)}\"");
                }
                writer.WriteLine(string.Join(",", values));
            }
        }

        private static string EscapeCsvField(string field)
        {
            if (field == null) return "";
            return field.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
        }
    }
}
