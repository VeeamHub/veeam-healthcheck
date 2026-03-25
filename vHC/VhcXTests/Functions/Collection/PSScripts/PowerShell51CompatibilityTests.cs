// PowerShell 5.1 Compatibility Tests
// Validates that VBR scripts don't contain PS7-only syntax or non-ASCII characters
// Background: Issue #97 - ternary operators broke PS5.1 compatibility
//             UTF-8 non-ASCII bytes (e.g. em dash) decoded as Windows-1252 by PS5.1, corrupting string parsing

using System.Text.RegularExpressions;
using Xunit;

namespace VhcXTests.Functions.Collection.PSScripts;

/// <summary>
/// Tests to ensure PowerShell scripts remain compatible with PS5.1.
/// VBR 12 and earlier use PowerShell 5.1, so scripts must avoid PS7-only syntax.
/// </summary>
public class PowerShell51CompatibilityTests
{
    // Scripts that MUST be PS5.1 compatible (used by VBR 12 and earlier)
    private static readonly string[] Ps51RequiredScripts = new[]
    {
        "Tools/Scripts/HealthCheck/VBR/Get-VBRConfig.ps1",
        "Tools/Scripts/HealthCheck/VBR/Get-NasInfo.ps1",
        "Tools/Scripts/HotfixDetection/Collect-VBRLogs.ps1",
        "Tools/Scripts/HotfixDetection/DumpManagedServerToText.ps1"
    };

    // PS7-only syntax patterns that must NOT appear in PS5.1-compatible scripts
    private static readonly (string Pattern, string Description)[] Ps7OnlyPatterns = new[]
    {
        (@"\s+\?\s+\$\w+\s*:\s*", "Ternary operator (condition ? value : alternative)"),
        (@"\?\?(?!=)", "Null-coalescing operator (??)"),
        (@"\?\?=", "Null-coalescing assignment (??=)"),
        (@"(?<!\|)\|\|(?!\|)(?!\s*$)", "Pipeline chain OR operator (||)"),
        (@"(?<!&)&&(?!&)(?!\s*$)", "Pipeline chain AND operator (&&)")
    };

    [Fact]
    public void GetVBRConfig_ShouldNotContain_TernaryOperator()
    {
        var scriptPath = GetScriptPath("Tools/Scripts/HealthCheck/VBR/Get-VBRConfig.ps1");
        AssertNoPs7Syntax(scriptPath, "ternary");
    }

    [Fact]
    public void GetNasInfo_ShouldNotContain_TernaryOperator()
    {
        var scriptPath = GetScriptPath("Tools/Scripts/HealthCheck/VBR/Get-NasInfo.ps1");
        AssertNoPs7Syntax(scriptPath, "ternary");
    }

    [Theory]
    [MemberData(nameof(GetAllPs51Scripts))]
    public void AllPs51Scripts_ShouldNotContain_AnyPs7OnlySyntax(string relativePath)
    {
        var scriptPath = GetScriptPath(relativePath);
        if (!File.Exists(scriptPath))
        {
            // Skip if script doesn't exist (may be removed in future)
            return;
        }

        AssertNoPs7Syntax(scriptPath, "all");
    }

    /// <summary>
    /// PS5.1 reads files as Windows-1252 by default (no BOM). Non-ASCII UTF-8 bytes such as an
    /// em dash (U+2014 = 0xE2 0x80 0x94) are reinterpreted — byte 0x94 becomes a right curly
    /// double-quote in Windows-1252, silently corrupting string parsing and producing cascading
    /// parse errors far from the actual offending character.
    /// All VBR scripts must therefore be pure ASCII.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetAllVbrScriptFiles))]
    public void AllVbrScripts_ShouldContainOnlyAsciiCharacters(string relativePath)
    {
        var scriptPath = GetVbrScriptsRootPath(relativePath);
        if (!File.Exists(scriptPath))
            return;

        var lines = File.ReadAllLines(scriptPath, System.Text.Encoding.Latin1);
        var violations = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            foreach (char c in lines[i])
            {
                if (c > 127)
                {
                    violations.Add($"Line {i + 1}: non-ASCII character U+{(int)c:X4} ('{c}')\n  {lines[i].Trim()}");
                    break;
                }
            }
        }

        if (violations.Count > 0)
        {
            Assert.Fail(
                $"Non-ASCII characters found in {relativePath}:\n\n" +
                string.Join("\n\n", violations) +
                "\n\nFix: Replace with ASCII equivalents (e.g. em dash \u2014 --> -)." +
                "\n     PS5.1 reads files as Windows-1252; non-ASCII UTF-8 bytes corrupt string parsing.");
        }
    }

    public static IEnumerable<object[]> GetAllVbrScriptFiles()
    {
        var root = GetVbrScriptsRoot();
        if (root == null)
            yield break;

        foreach (var file in Directory.EnumerateFiles(root, "*.ps1", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(root, "*.psm1", SearchOption.AllDirectories)))
        {
            yield return new object[] { Path.GetRelativePath(root, file) };
        }
    }

    public static IEnumerable<object[]> GetAllPs51Scripts()
    {
        foreach (var script in Ps51RequiredScripts)
        {
            yield return new object[] { script };
        }
    }

    private static string? GetVbrScriptsRoot()
    {
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var searchDir = new DirectoryInfo(currentDir);

        while (searchDir != null && searchDir.Name != "vHC")
            searchDir = searchDir.Parent;

        if (searchDir == null)
            return null;

        return Path.Combine(searchDir.FullName, "HC_Reporting", "Tools", "Scripts", "HealthCheck", "VBR");
    }

    private static string GetVbrScriptsRootPath(string relativePath)
    {
        var root = GetVbrScriptsRoot() ?? throw new DirectoryNotFoundException("Could not find VBR scripts directory");
        return Path.Combine(root, relativePath);
    }

    private static string GetScriptPath(string relativePath)
    {
        // Walk up from test assembly to find the HC_Reporting directory
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var searchDir = new DirectoryInfo(currentDir);

        while (searchDir != null && searchDir.Name != "vHC")
        {
            searchDir = searchDir.Parent;
        }

        if (searchDir == null)
        {
            throw new DirectoryNotFoundException("Could not find vHC directory");
        }

        return Path.Combine(searchDir.FullName, "HC_Reporting", relativePath);
    }

    private static void AssertNoPs7Syntax(string scriptPath, string checkType)
    {
        if (!File.Exists(scriptPath))
        {
            Assert.Fail($"Script not found: {scriptPath}");
            return;
        }

        var content = File.ReadAllText(scriptPath);
        var lines = File.ReadAllLines(scriptPath);
        var violations = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNum = i + 1;

            // Skip comment lines
            if (line.TrimStart().StartsWith("#"))
            {
                continue;
            }

            foreach (var (pattern, description) in Ps7OnlyPatterns)
            {
                if (checkType != "all" && !description.ToLower().Contains(checkType.ToLower()))
                {
                    continue;
                }

                if (Regex.IsMatch(line, pattern))
                {
                    violations.Add($"Line {lineNum}: {description}\n  {line.Trim()}");
                }
            }
        }

        if (violations.Count > 0)
        {
            var message = $"PS7-only syntax found in {Path.GetFileName(scriptPath)}:\n\n" +
                          string.Join("\n\n", violations) +
                          "\n\nFix: Replace with PS5.1-compatible syntax:\n" +
                          "  Ternary:    $x ? $a : $b  -->  if ($x) { $a } else { $b }\n" +
                          "  Null-coal:  $x ?? $y      -->  if ($null -eq $x) { $y } else { $x }\n" +
                          "  Chain ops:  cmd1 && cmd2  -->  cmd1; if ($?) { cmd2 }";

            Assert.Fail(message);
        }
    }
}
