using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using VeeamHealthCheck;
using VeeamHealthCheck.Functions.Collection;
using VeeamHealthCheck.Functions.Reporting.Html.VBR;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
using Xunit;

namespace VhcXTests.TestData;

/// <summary>
/// Runs the VBR compiler/exporter used by /import, with copied golden inputs.
/// Callers must belong to GlobalState. Reflection is limited to saving/restoring
/// private process state for which the application provides no reset API.
/// </summary>
internal sealed class GoldenVbrReport : IDisposable
{
    private readonly Dictionary<FieldInfo, object?> globals = typeof(CGlobals)
        .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(field => !field.IsInitOnly && !field.IsLiteral)
        .ToDictionary(field => field, field => field.GetValue(null));
    private readonly string? resolvedPath = CVariables.ResolvedImportPath;
    private readonly string logPath = CGlobals.Logger.logFile;
    private readonly CultureInfo culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo uiCulture = CultureInfo.CurrentUICulture;
    private readonly string? experiments = Environment.GetEnvironmentVariable("VHC_EXPERIMENTS");
    private readonly FieldInfo matchesField = ScrubField("matchDictionary");
    private readonly Dictionary<string, string> matches;
    private readonly Dictionary<string, int> counters;
    private readonly XDocument document;
    private readonly XElement savedRoot;

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "VhcGoldenReport_" + Guid.NewGuid().ToString("N"));
    public string Input => Path.Combine(Root, "input");
    public string HtmlPath { get; private set; } = string.Empty;
    public string JsonPath => Path.ChangeExtension(HtmlPath, ".json");

    public GoldenVbrReport()
    {
        matches = (Dictionary<string, string>)matchesField.GetValue(CGlobals.Scrubber)!;
        counters = new Dictionary<string, int>((Dictionary<string, int>)ScrubField("typeCounters").GetValue(CGlobals.Scrubber)!);
        document = (XDocument)ScrubField("doc").GetValue(CGlobals.Scrubber)!;
        savedRoot = new XElement(document.Root!);
        try
        {
            Directory.CreateDirectory(Input);
            Directory.CreateDirectory(Path.Combine(Root, "Original"));
            foreach (string file in Directory.GetFiles(FindGoldenDirectory(), "*.csv"))
                File.Copy(file, Path.Combine(Input, Path.GetFileName(file)));

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Environment.SetEnvironmentVariable("VHC_EXPERIMENTS", null);
            CGlobals.desiredPath = Root;
            SetLogPath(Path.Combine(Root, "report.log"));
            CGlobals.IMPORT = true;
            CGlobals.IMPORT_PATH = Input;
            CVariables.ResolvedImportPath = CImportPathResolver.FindCsvDirectory(Input);
            Assert.Equal(Input, CVariables.ResolvedImportPath);
            CGlobals.REMOTEEXEC = false;
            CGlobals.REMOTEHOST = string.Empty;
            CGlobals.VBRServerName = "golden-fixture-server";
            CGlobals.VBRMAJORVERSION = 12;
            CGlobals.VBRFULLVERSION = "12.1.2.172";
            CGlobals.VHCVERSION = "1.2.3.4";
            CGlobals.TOOLSTART = new DateTime(2024, 3, 2, 12, 0, 0);
            CGlobals.ReportDays = 7;
            CGlobals.GUIEXEC = false;
            CGlobals.OpenHtml = false;
            CGlobals.OpenExplorer = false;
            CGlobals.EXPORTPDF = false;
            CGlobals.EXPORTPPTX = false;
            CGlobals.EXPORTINDIVIDUALJOBHTMLS = false;
            CGlobals.FullReportJson = new();
            CGlobals.CsvValidationResults = new();
            CGlobals.CollectionManifest = new();
            CGlobals.ServerInfo = new();
            CGlobals.DEFAULTREGISTRYKEYS = new();
            CGlobals.IsReconDetected = false;
            CGlobals.LastReconRun = DateTime.MinValue;
            ResetScrubber();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public string Generate(bool scrub, bool requestPptx = false, bool requestPdf = false)
    {
        CGlobals.Scrub = scrub;
        CGlobals.EXPORTPPTX = requestPptx;
        CGlobals.EXPORTPDF = requestPdf;
        CGlobals.FullReportJson = new();
        ResetScrubber();
        var compiler = new CHtmlCompiler();
        try
        {
            Assert.Equal(0, compiler.RunFullVbrReport());
        }
        finally
        {
            compiler.Dispose();
        }

        string output = Path.Combine(Root, scrub ? "vHC-AnonymousReport" : "vHC-Report");
        HtmlPath = Assert.Single(Directory.GetFiles(output, "*_VBR_*.html"));
        Assert.True(File.Exists(JsonPath), "VBR import must export JSON alongside HTML.");
        return File.ReadAllText(HtmlPath);
    }

    private void ResetScrubber()
    {
        matchesField.SetValue(CGlobals.Scrubber, new Dictionary<string, string>());
        ((Dictionary<string, int>)ScrubField("typeCounters").GetValue(CGlobals.Scrubber)!).Clear();
        document.Root!.RemoveAll();
    }

    private static FieldInfo ScrubField(string name) =>
        typeof(CScrubHandler).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(CScrubHandler).FullName, name);

    private static void SetLogPath(string path) =>
        typeof(CLogger).GetProperty(nameof(CLogger.logFile))!.SetValue(CGlobals.Logger, path);

    private static string FindGoldenDirectory()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            string product = Path.Combine(directory.FullName, "vHC", "HC_Reporting");
            if (Directory.Exists(product))
                return Path.Combine(product, "Tools", "GoldenBaselines", "VBRConfig");
        }
        throw new DirectoryNotFoundException("Cannot locate vHC/HC_Reporting above AppContext.BaseDirectory.");
    }

    public void Dispose()
    {
        matchesField.SetValue(CGlobals.Scrubber, matches);
        var currentCounters = (Dictionary<string, int>)ScrubField("typeCounters").GetValue(CGlobals.Scrubber)!;
        currentCounters.Clear();
        foreach (KeyValuePair<string, int> counter in counters)
            currentCounters.Add(counter.Key, counter.Value);
        document.Root!.ReplaceWith(new XElement(savedRoot));
        SetLogPath(logPath);
        foreach (KeyValuePair<FieldInfo, object?> state in globals)
            state.Key.SetValue(null, state.Value);
        CVariables.ResolvedImportPath = resolvedPath;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = uiCulture;
        Environment.SetEnvironmentVariable("VHC_EXPERIMENTS", experiments);
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }
}
