// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using VeeamHealthCheck.Functions.Collection.PSCollections;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Guards what the shipped package contains: only what the tool needs at runtime, and every
    /// script the tool runs is a readable file on disk (nothing hidden inside the exe).
    /// </summary>
    [Trait("Category", "Packaging")]
    public class PackageContentsTests
    {
        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "HC_Reporting"));

        private static readonly string[] ScriptExtensions = { ".ps1", ".psm1", ".psd1", ".bat", ".cmd", ".sh" };

        private static XDocument LoadCsproj() =>
            XDocument.Load(Path.Combine(ProjectRoot, "VeeamHealthCheck.csproj"));

        private static string Norm(string? s) => (s ?? string.Empty).Replace('\\', '/');

        [Fact]
        public void Csproj_EmbeddedResources_ContainNoExecutableScripts()
        {
            var embedded = LoadCsproj().Descendants("EmbeddedResource")
                .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
                .ToList();

            Assert.NotEmpty(embedded);
            var scripts = embedded
                .Where(i => ScriptExtensions.Contains(Path.GetExtension(Norm(i)).ToLowerInvariant()))
                .ToList();

            Assert.True(scripts.Count == 0,
                "Scripts must ship as plain files so a reviewer can read them, never as EmbeddedResource: " +
                string.Join(", ", scripts));
        }

        [Fact]
        public void Csproj_ModuleContentGlob_ExcludesValidationHarnessAndTests()
        {
            var moduleItems = LoadCsproj().Descendants("Content")
                .Where(c => Norm((string?)c.Attribute("Include")).Contains("HealthCheck/VBR/vHC-VbrConfig/"))
                .ToList();

            Assert.NotEmpty(moduleItems);
            foreach (var item in moduleItems)
            {
                var exclude = Norm((string?)item.Attribute("Exclude"));
                Assert.Contains("vHC-VbrConfig/Validation/**", exclude);
                Assert.Contains("*.Tests.ps1", exclude);
            }
        }

        [Fact]
        public void Csproj_NoContentItem_PullsInValidationHarness()
        {
            // Any other Content/None item that would copy the harness to output defeats the exclusion.
            var offenders = LoadCsproj().Descendants()
                .Where(e => e.Name == "Content" || e.Name == "None" || e.Name == "EmbeddedResource")
                .Where(e => e.Attribute("Include") != null)
                .Where(e => Norm((string)e.Attribute("Include")!).Contains("/Validation/"))
                .Where(e => !string.Equals((string?)e.Element("CopyToOutputDirectory"), "Never", StringComparison.OrdinalIgnoreCase))
                .Select(e => (string)e.Attribute("Include")!)
                .ToList();

            Assert.Empty(offenders);
        }

        [Fact]
        public void Vb365Collector_HasSingleSourceOfTruth()
        {
            var copies = Directory.EnumerateFiles(ProjectRoot, "Collect-VB365Data.ps1", SearchOption.AllDirectories)
                .Select(Norm)
                .Where(p => !p.Contains("/bin/") && !p.Contains("/obj/"))
                .ToList();

            Assert.Single(copies);
            Assert.EndsWith("Tools/Scripts/HealthCheck/VB365/Collect-VB365Data.ps1", copies[0]);
        }

        [Fact]
        public void Vb365Collector_RunByTheApp_IsTheFileCopiedToOutput()
        {
            var field = typeof(PSInvoker).GetField("vb365Script", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            var path = Norm((string?)field!.GetValue(new PSInvoker()));

            var relative = path.Substring(Norm(AppDomain.CurrentDomain.BaseDirectory).Length).TrimStart('/');
            Assert.Equal("Tools/Scripts/HealthCheck/VB365/Collect-VB365Data.ps1", relative);
            Assert.True(File.Exists(Path.Combine(ProjectRoot, relative.Replace('/', Path.DirectorySeparatorChar))),
                "The script the app runs must exist as a plain file in the source tree");

            var copied = LoadCsproj().Descendants("Content")
                .Any(c => string.Equals(Norm((string?)c.Attribute("Include")), relative, StringComparison.OrdinalIgnoreCase)
                          && !string.Equals((string?)c.Element("CopyToOutputDirectory"), "Never", StringComparison.OrdinalIgnoreCase));
            Assert.True(copied, "The script the app runs must be copied to the output/package");
        }
    }
}
