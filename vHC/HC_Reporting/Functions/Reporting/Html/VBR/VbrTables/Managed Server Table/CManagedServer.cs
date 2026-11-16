// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
namespace VeeamHealthCheck.Reporting.Html.VBR
{
    public class CManagedServer
    {
        public string Name { get; set; }

        public int Cores { get; set; }

        public int Ram { get; set; }

        public string Type { get; set; }

        public string OsInfo { get; set; }

        public string ApiVersion { get; set; }

        public int ProtectedVms { get; set; }

        public int NotProtectedVms { get; set; }

        public int TotalVms { get; set; }

        public bool IsProxy { get; set; }

        public bool IsRepo { get; set; }

        public bool IsWan { get; set; }

        public string IsUnavailable { get; set; }

        public string Platform { get; set; }

        /// <summary>VBR Location the server is assigned to; "" when none or unknown.</summary>
        public string Location { get; set; } = string.Empty;
    }
}
