using System.Collections.Generic;

namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    internal class HtmlSection
    {
        public string SectionName { get; set; }

        public List<string> Headers { get; set; } = new();

        public List<List<string>> Rows { get; set; } = new();

        public string Summary { get; set; }
    }
}
