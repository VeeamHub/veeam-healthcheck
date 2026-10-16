// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
// Guards the verify-vhc CLI feature map against drift from CArgsParser.

using System.Text.RegularExpressions;
using Xunit;

namespace VhcXTests.CrossPlatform;

/// <summary>
/// Fails when the flags CArgsParser.ParseAllArgs recognizes and the flags listed in
/// .claude/skills/verify-vhc/features/cli-arguments.md disagree in either direction.
///
/// Code side: CArgsParser has no enumerable flag list and cannot be compiled into this
/// project (it pulls in Avalonia and the PowerShell SDK), so the test reads the source and
/// takes every flag named on a <c>case</c> label inside ParseAllArgs. A label may be a
/// literal (<c>case "/run":</c>) or a pattern guard (<c>case var m when new Regex("/outdir=.*")</c>
/// or <c>a.StartsWith("/credfile=")</c>). Flags are compared by base name: the text from the
/// slash up to the first ':' or '=' (so /days:7 and /days:30 are both "/days").
///
/// Doc side: the first backticked token of each table row between the
/// <c>cli-flags:start</c> and <c>cli-flags:end</c> HTML comments, reduced to the same base name.
/// </summary>
public class CliArgumentMapTests
{
    private const string ParserRelPath = "vHC/HC_Reporting/Startup/CArgsParser.cs";
    private const string DocRelPath = ".claude/skills/verify-vhc/features/cli-arguments.md";

    [Fact]
    public void CliArgumentMap_MatchesParser()
    {
        string root = FindRepoRoot();
        var code = ExtractParserFlags(File.ReadAllText(Path.Combine(root, ParserRelPath)));
        var doc = ExtractDocFlags(File.ReadAllText(Path.Combine(root, DocRelPath)));

        var undocumented = code.Except(doc).OrderBy(x => x).ToList();
        var stale = doc.Except(code).OrderBy(x => x).ToList();

        var problems = new List<string>();
        if (undocumented.Count > 0)
        {
            problems.Add($"Recognized by {ParserRelPath} but missing from the flag table in {DocRelPath}: " +
                         $"{string.Join(", ", undocumented)}. Add a row for each to {DocRelPath}.");
        }

        if (stale.Count > 0)
        {
            problems.Add($"Listed in {DocRelPath} but not recognized by {ParserRelPath} (ParseAllArgs): " +
                         $"{string.Join(", ", stale)}. Remove or fix the row in {DocRelPath}, or restore the flag in the parser.");
        }

        Assert.True(problems.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void ParserExtraction_FindsKnownFlags()
    {
        // Sanity check that the source scan itself has not silently broken (e.g. the parser was
        // restructured): an empty or tiny set would make the drift test vacuously pass.
        var code = ExtractParserFlags(File.ReadAllText(Path.Combine(FindRepoRoot(), ParserRelPath)));
        Assert.Contains("/run", code);
        Assert.Contains("/outdir", code);
        Assert.Contains("/import", code);
        Assert.Contains("/host", code);
        Assert.True(code.Count >= 15, $"Only {code.Count} flags extracted from ParseAllArgs; the scan likely needs updating.");
    }

    internal static HashSet<string> ExtractParserFlags(string source)
    {
        source = source.Replace("\r\n", "\n");
        int start = source.IndexOf("private int ParseAllArgs(", StringComparison.Ordinal);
        Assert.True(start >= 0, $"ParseAllArgs not found in {ParserRelPath}; update {nameof(CliArgumentMapTests)}.");

        // Body ends at the first closing brace indented like the method signature.
        int end = source.IndexOf("\n        }\n", start, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find the end of ParseAllArgs in {ParserRelPath}.");

        var flags = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in source[start..end].Split('\n'))
        {
            string t = line.TrimStart();
            if (!t.StartsWith("case ", StringComparison.Ordinal)) continue;
            foreach (Match m in Regex.Matches(t, "\"\\^?(/[A-Za-z0-9]+)"))
            {
                flags.Add(m.Groups[1].Value.ToLowerInvariant());
            }
        }

        return flags;
    }

    internal static HashSet<string> ExtractDocFlags(string markdown)
    {
        var m = Regex.Match(markdown.Replace("\r\n", "\n"), @"<!--\s*cli-flags:start\s*-->(.*?)<!--\s*cli-flags:end\s*-->", RegexOptions.Singleline);
        Assert.True(m.Success, $"{DocRelPath} must contain a flag table between <!-- cli-flags:start --> and <!-- cli-flags:end --> comments.");

        var flags = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in m.Groups[1].Value.Split('\n'))
        {
            var row = Regex.Match(line, @"^\s*\|\s*`(/[A-Za-z0-9]+)");
            if (row.Success) flags.Add(row.Groups[1].Value.ToLowerInvariant());
        }

        return flags;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ParserRelPath))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate the repo root (a directory containing {ParserRelPath}) above the test output directory.");
    }
}
