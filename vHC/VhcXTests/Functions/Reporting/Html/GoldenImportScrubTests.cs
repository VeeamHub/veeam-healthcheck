using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CsvHelper;
using VhcXTests.TestData;
using Xunit;
using Xunit.Abstractions;

namespace VhcXTests.Functions.Reporting.Html;

[Collection("GlobalState")]
public sealed class GoldenImportScrubTests
{
    private readonly ITestOutputHelper output;

    public GoldenImportScrubTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void RunFullVbrReport_ScrubbedGoldenImport_RemovesEveryIdentifierColumnValueFromHtml()
    {
        using var report = new GoldenVbrReport();
        Identifier[] identifiers = ReadIdentifiers(report.Input, out int columnCount);
        Assert.NotEmpty(identifiers);
        string plain = WebUtility.HtmlDecode(report.Generate(scrub: false));
        Assert.Contains(identifiers, identifier => plain.Contains(identifier.Value, StringComparison.OrdinalIgnoreCase));
        string html = WebUtility.HtmlDecode(report.Generate(scrub: true));
        Identifier[] leaks = identifiers.Where(identifier => html.Contains(identifier.Value, StringComparison.OrdinalIgnoreCase)).ToArray();
        output.WriteLine($"Checked {columnCount} columns, {identifiers.Length} column/value pairs, {identifiers.Select(item => item.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count()} distinct identifiers.");
        Assert.True(leaks.Length == 0, "HTML identifier leaks:\n" + string.Join("\n", leaks.Select(identifier => $"{identifier.File}:{identifier.Column} = {identifier.Value}")));
    }

    [Fact]
    public void RunFullVbrReport_ScrubbedGoldenImport_RemovesEveryIdentifierColumnValueFromHtmlAndJson()
    {
        using var report = new GoldenVbrReport();
        Identifier[] identifiers = ReadIdentifiers(report.Input, out int columnCount);
        Assert.NotEmpty(identifiers);
        string plainHtml = WebUtility.HtmlDecode(report.Generate(scrub: false));
        string plainJson = DecodeJson(File.ReadAllText(report.JsonPath));
        string scrubbedHtml = WebUtility.HtmlDecode(report.Generate(scrub: true));
        string scrubbedJson = DecodeJson(File.ReadAllText(report.JsonPath));
        output.WriteLine($"Identifier columns: {columnCount}; distinct column/value pairs: {identifiers.Length}; distinct values: {identifiers.Select(item => item.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");

        var leaks = new List<string>();
        foreach (Identifier identifier in identifiers)
        {
            bool inHtml = scrubbedHtml.Contains(identifier.Value, StringComparison.OrdinalIgnoreCase);
            bool inJson = scrubbedJson.Contains(identifier.Value, StringComparison.OrdinalIgnoreCase);
            if (inHtml || inJson)
                leaks.Add($"{identifier.File}:{identifier.Column} = {identifier.Value} [{(inHtml ? "HTML" : "")}{(inHtml && inJson ? ", " : "")}{(inJson ? "JSON" : "")}]");
        }
        output.WriteLine("Identifiers present in plain outputs: " + identifiers.Count(item =>
            plainHtml.Contains(item.Value, StringComparison.OrdinalIgnoreCase) || plainJson.Contains(item.Value, StringComparison.OrdinalIgnoreCase)));
        output.WriteLine(string.Join(Environment.NewLine, leaks));
        Assert.True(leaks.Count == 0, "Identifier leaks:\n" + string.Join("\n", leaks));
    }

    [Fact]
    public void RunFullVbrReport_ScrubbedGoldenImport_PreservesNumbersAndWritesReversibleKeyFiles()
    {
        using var report = new GoldenVbrReport();
        Identifier[] identifiers = ReadIdentifiers(report.Input, out int columnCount);
        Assert.NotEmpty(identifiers);
        Assert.True(columnCount > 0);
        string html = report.Generate(scrub: true);
        string json = DecodeJson(File.ReadAllText(report.JsonPath));
        Assert.Contains("12.1.2.172", html);
        Assert.Contains("12.1.2.172", json);
        Assert.Contains("512.45", html);
        Assert.Contains("512.45", json);

        string keyPath = Assert.Single(Directory.GetFiles(report.Root, "*vHC_KeyFile.json", SearchOption.AllDirectories));
        string xmlPath = Assert.Single(Directory.GetFiles(report.Root, "*vHC_KeyFile.xml", SearchOption.AllDirectories));
        Dictionary<string, string> key = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(keyPath))!;
        Assert.NotEmpty(key);
        var xml = XDocument.Load(xmlPath);
        Dictionary<string, string> xmlKey = xml.Root!.Elements("fauxname").ToDictionary(
            element => string.Concat(element.Nodes().OfType<XText>().Select(text => text.Value)),
            element => element.Element("originalname")!.Value);
        Assert.Equal(key.OrderBy(pair => pair.Key), xmlKey.OrderBy(pair => pair.Key));
        KeyValuePair<string, string>[] mapped = key.Where(pair => identifiers.Any(identifier => identifier.Value == pair.Value)).ToArray();
        Assert.NotEmpty(mapped);
        Assert.Contains(mapped, pair => pair.Value == "Production-VM-Backup");
        Assert.Contains(mapped, pair => pair.Value == "Linux Hardened Repo");
        Assert.Contains(mapped, pair => pair.Value == @"VEEAM\admin");
        foreach (KeyValuePair<string, string> pair in mapped)
        {
            Assert.NotEqual(pair.Key, pair.Value);
            Assert.Contains("_", pair.Key);
        }
        Assert.Contains(mapped, pair => html.Contains(pair.Key, StringComparison.Ordinal) && json.Contains(pair.Key, StringComparison.Ordinal));
        output.WriteLine($"Enumerated {columnCount} columns, {identifiers.Length} column/value pairs, {identifiers.Select(item => item.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count()} distinct identifiers; {mapped.Length} reversible fixture mappings.");
    }

    [Fact]
    public void RunFullVbrReport_ScrubbedImportWithPdfAndPptxRequested_ExportsOnlyHtmlAndJsonAndWarns()
    {
        using var report = new GoldenVbrReport();
        Assert.Contains("<table", report.Generate(scrub: true, requestPptx: true, requestPdf: true));
        Assert.Empty(Directory.GetFiles(report.Root, "*.pptx", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(report.Root, "*.pdf", SearchOption.AllDirectories));
        Assert.True(File.Exists(report.JsonPath));
        string log = File.ReadAllText(Path.Combine(report.Root, "report.log"));
        Assert.Contains("PDF export skipped: it is not available for scrubbed reports", log);
        Assert.Contains("PowerPoint export skipped: it is not available for scrubbed reports", log);
    }

    // Match human-readable identity fields, not arbitrary substrings such as "ip" in
    // Description or "vm" in retention flags. Exclude GUID foreign keys (*Id/*Ids),
    // registry KeyName and malware ThreatName (software signatures, not customer identities).
    // Include SQL database/instance and license-owner fields as well as names/hosts/IPs/
    // paths/users/domains/VMs/repos/tenants. Semicolon-separated lists are also checked
    // member-by-member. Blank, <4-character, boolean, numeric, and generic missing-value
    // sentinels are excluded; no fixture-specific names are exempted.
    private static readonly Regex IdentifierHeader = new(
        @"(?:name|hosts?|server|ip(?:address|start|end)?|path|folder|user(?:name)?|domain|fqdn|dns|vms?|repo(?:sitory)?|proxy|tenant)$|^(?:licensedto|createdby|sqlserver|mshost|pghost|msdb|pgdb|instance|target)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static Identifier[] ReadIdentifiers(string input, out int columnCount)
    {
        var values = new HashSet<Identifier>();
        columnCount = 0;
        foreach (string path in Directory.GetFiles(input, "*.csv").OrderBy(path => path, StringComparer.Ordinal))
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            Assert.True(csv.Read());
            csv.ReadHeader();
            string[] headers = csv.HeaderRecord!;
            int[] columns = Enumerable.Range(0, headers.Length).Where(index => IsIdentifierHeader(headers[index])).ToArray();
            columnCount += columns.Length;
            while (csv.Read())
            {
                foreach (int index in columns)
                {
                    string value = (csv.GetField(index) ?? "").Trim();
                    foreach (string item in new[] { value }.Concat(value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)))
                    {
                        if (IsSpecificValue(item))
                            values.Add(new Identifier(Path.GetFileName(path), headers[index], item));
                    }
                }
            }
        }
        return values.OrderBy(item => item.File, StringComparer.Ordinal).ThenBy(item => item.Column, StringComparer.Ordinal)
            .ThenBy(item => item.Value, StringComparer.Ordinal).ToArray();
    }

    private static bool IsIdentifierHeader(string header) =>
        !header.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
        && !header.EndsWith("Ids", StringComparison.OrdinalIgnoreCase)
        && !header.Equals("KeyName", StringComparison.OrdinalIgnoreCase)
        && !header.Equals("ThreatName", StringComparison.OrdinalIgnoreCase)
        && IdentifierHeader.IsMatch(header);

    private static bool IsSpecificValue(string value) =>
        value.Length >= 4 && !bool.TryParse(value, out _)
        && !decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
        && !new[] { "None", "Null", "Unknown", "Undefined", "System.Object[]" }.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static string DecodeJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return string.Join("\n", Strings(document.RootElement).Select(WebUtility.HtmlDecode));
    }

    private static IEnumerable<string> Strings(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            yield return element.GetString()!;
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
                foreach (string value in Strings(child))
                    yield return value;
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
                foreach (string value in Strings(property.Value))
                    yield return value;
        }
        else if (element.ValueKind == JsonValueKind.Number)
            yield return element.GetRawText();
    }

    private sealed record Identifier(string File, string Column, string Value);
}
