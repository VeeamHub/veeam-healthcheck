using System.Net;
using VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Jobs_Info;
using VhcXTests.TestData;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html;

[Collection("GlobalState")]
public sealed class JobInfoNasSizeTests
{
    private const string NasJobName = "NAS-Orphaned-Repo-Job";
    private const string ImportFailedMessage = "Job Info Data import failed";

    [Fact]
    public void RunFullVbrReport_NasJobWithNasBackupCsvMissing_RendersJobInfoWithoutImportError()
    {
        using var report = new GoldenVbrReport();
        AddNasJobToJobsCsv(report.Input, NasJobName);
        DeleteNasBackupCsv(report.Input);

        string html = WebUtility.HtmlDecode(report.Generate(scrub: false));

        AssertNoImportError(report);
        Assert.Contains(NasJobName, html);
    }

    [Fact]
    public void RunFullVbrReport_NasJobAbsentFromNasBackupCsv_RendersJobInfoWithoutImportError()
    {
        using var report = new GoldenVbrReport();
        AddNasJobToJobsCsv(report.Input, NasJobName);

        string html = WebUtility.HtmlDecode(report.Generate(scrub: false));

        AssertNoImportError(report);
        Assert.Contains(NasJobName, html);
    }

    [Fact]
    public void RunFullVbrReport_NasJobPresentInNasBackupCsv_ShowsSizeFromCsv()
    {
        using var report = new GoldenVbrReport();
        AddNasJobToJobsCsv(report.Input, "NAS-FileShare-Backup");

        string html = WebUtility.HtmlDecode(report.Generate(scrub: false));

        AssertNoImportError(report);
        Assert.Contains("512", html);
    }

    [Fact]
    public void RunFullVbrReport_NasBackupCsvWithoutSizeColumns_RendersJobInfoWithoutImportError()
    {
        using var report = new GoldenVbrReport();
        AddNasJobToJobsCsv(report.Input, "NAS-FileShare-Backup");
        ReplaceNasBackupCsv(
            report.Input,
            "\"Id\",\"Name\",\"Description\"" + Environment.NewLine
            + "\"eeee5555-ffff-0000-1111-222255555555\",\"NAS-FileShare-Backup\",\"File share backup job\"" + Environment.NewLine);

        string html = WebUtility.HtmlDecode(report.Generate(scrub: false));

        AssertNoImportError(report);
        Assert.Contains("NAS-FileShare-Backup", html);
    }

    [Fact]
    public void RunFullVbrReport_NasBackupCsvWithoutNameColumn_RendersJobInfoWithoutImportError()
    {
        using var report = new GoldenVbrReport();
        AddNasJobToJobsCsv(report.Input, NasJobName);
        ReplaceNasBackupCsv(
            report.Input,
            "\"Id\",\"Description\"" + Environment.NewLine
            + "\"eeee5555-ffff-0000-1111-222255555555\",\"File share backup job\"" + Environment.NewLine);

        string html = WebUtility.HtmlDecode(report.Generate(scrub: false));

        AssertNoImportError(report);
        Assert.Contains(NasJobName, html);
    }

    private static void AssertNoImportError(GoldenVbrReport report)
    {
        string[] lines = File.ReadAllLines(Path.Combine(report.Root, "report.log"));
        int failed = Array.FindIndex(lines, line => line.Contains(ImportFailedMessage, StringComparison.Ordinal));
        string detail = failed < 0 ? string.Empty : string.Join(Environment.NewLine, lines.Skip(failed).Take(3));
        Assert.True(failed < 0, "Job Info import logged an error:" + Environment.NewLine + detail);
    }

    private static void ReplaceNasBackupCsv(string input, string content)
    {
        string path = Assert.Single(Directory.GetFiles(input, "*nasBackup.csv"));
        File.WriteAllText(path, content);
    }

    private static void DeleteNasBackupCsv(string input)
    {
        string[] files = Directory.GetFiles(input, "*nasBackup.csv");
        Assert.Single(files);
        File.Delete(files[0]);
    }

    // Appends a NasBackup row to the copied _Jobs.csv by reusing the columns of the first golden row.
    private static void AddNasJobToJobsCsv(string input, string name)
    {
        string jobsCsv = Assert.Single(Directory.GetFiles(input, "*_Jobs.csv"));
        string template = File.ReadAllLines(jobsCsv)[1];
        int afterName = template.IndexOf("\",\"", StringComparison.Ordinal);
        int afterType = template.IndexOf("\",\"", afterName + 3, StringComparison.Ordinal);
        string nasRow = "\"" + name + "\",\"NasBackup" + template.Substring(afterType);
        File.AppendAllText(jobsCsv, Environment.NewLine + nasRow + Environment.NewLine);
    }
}
