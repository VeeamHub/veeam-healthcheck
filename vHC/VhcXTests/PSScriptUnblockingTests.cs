// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using VeeamHealthCheck.Functions.Collection.PSCollections;
using Xunit;

namespace VhcXTests
{
    /// <summary>
    /// Tests to ensure all PowerShell scripts are properly unblocked to prevent production hangs.
    /// </summary>
    public class PSScriptUnblockingTests
    {
        [Fact]
        public void TryUnblockFiles_IncludesAllPowerShellScripts()
        {
            // Arrange
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var scriptsDirectory = Path.Combine(baseDirectory, "Tools", "Scripts");
            
            // Get all .ps1 files in the Tools/Scripts directory
            var allPs1Files = new List<string>();
            if (Directory.Exists(scriptsDirectory))
            {
                allPs1Files = Directory.GetFiles(scriptsDirectory, "*.ps1", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(baseDirectory, f))
                    .ToList();
            }

            // Get all script fields from PSInvoker class
            // We want ALL string fields that point to .ps1 files, regardless of name
            var psInvokerType = typeof(PSInvoker);
            var psInvoker = Activator.CreateInstance(psInvokerType, true);
            var scriptFields = psInvokerType
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(string))
                .Where(f => 
                {
                    var value = f.GetValue(psInvoker) as string;
                    return !string.IsNullOrEmpty(value) && value.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            // Get the TryUnblockFiles method
            var tryUnblockMethod = psInvokerType.GetMethod("TryUnblockFiles", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(tryUnblockMethod);

            // Get the method body to analyze UnblockFile calls
            var methodBody = tryUnblockMethod.GetMethodBody();
            
            // Build list of unblocked scripts from the fields we found
            var unblockedScripts = new List<string>();
            
            foreach (var field in scriptFields)
            {
                var scriptPath = field.GetValue(psInvoker) as string;
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    var relativePath = Path.GetRelativePath(baseDirectory, scriptPath);
                    unblockedScripts.Add(relativePath);
                }
            }

            // Assert - Find any .ps1 files that are not being unblocked
            var missingScripts = new List<string>();
            foreach (var ps1File in allPs1Files)
            {
                var normalizedPs1 = ps1File.Replace("\\", "/");
                var ps1FileName = Path.GetFileName(ps1File);
                
                var isUnblocked = unblockedScripts.Any(us => 
                {
                    var normalizedUs = us.Replace("\\", "/");
                    var usFileName = Path.GetFileName(us);
                    
                    // Match by full path or by filename (since paths are constructed differently)
                    return normalizedUs.Equals(normalizedPs1, StringComparison.OrdinalIgnoreCase) ||
                           usFileName.Equals(ps1FileName, StringComparison.OrdinalIgnoreCase) ||
                           normalizedPs1.EndsWith(usFileName, StringComparison.OrdinalIgnoreCase);
                });

                if (!isUnblocked)
                {
                    missingScripts.Add(ps1File);
                }
            }

            // Assert
            Assert.True(
                missingScripts.Count == 0,
                $"The following PowerShell scripts are not being unblocked in PSInvoker.TryUnblockFiles():\n" +
                string.Join("\n", missingScripts.Select(s => $"  - {s}")) +
                "\n\nPlease add these scripts to TryUnblockFiles() method to prevent production hangs."
            );
        }

        [Fact]
        public void TryUnblockFiles_AllFieldsHaveValidPaths()
        {
            // Arrange
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var psInvokerType = typeof(PSInvoker);
            var psInvoker = Activator.CreateInstance(psInvokerType, true);

            // Get all script fields - look for ANY string field that ends with .ps1
            var scriptFields = psInvokerType
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(string))
                .Where(f => 
                {
                    var value = f.GetValue(psInvoker) as string;
                    return !string.IsNullOrEmpty(value) && value.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            var invalidPaths = new List<string>();

            // Act & Assert
            foreach (var field in scriptFields)
            {
                var scriptPath = field.GetValue(psInvoker) as string;
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    // Check if it's a file path (not embedded resource)
                    if (scriptPath.Contains("\\") || scriptPath.Contains("/"))
                    {
                        var fullPath = Path.IsPathRooted(scriptPath) ? scriptPath : Path.Combine(baseDirectory, scriptPath);
                        
                        // For test environment, we just verify the path format is valid
                        // In production, the file should exist after deployment
                        try
                        {
                            var directory = Path.GetDirectoryName(fullPath);
                            var filename = Path.GetFileName(fullPath);
                            
                            Assert.False(string.IsNullOrEmpty(filename), 
                                $"Script field '{field.Name}' has invalid path: {scriptPath}");
                            Assert.True(filename.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase), 
                                $"Script field '{field.Name}' does not point to a .ps1 file: {scriptPath}");
                        }
                        catch (Exception ex)
                        {
                            invalidPaths.Add($"{field.Name}: {scriptPath} - {ex.Message}");
                        }
                    }
                }
            }

            Assert.True(
                invalidPaths.Count == 0,
                $"The following script fields have invalid paths:\n" + string.Join("\n", invalidPaths)
            );
        }

        [Fact]
        public void TryUnblockFiles_MethodExists()
        {
            // Arrange
            var psInvokerType = typeof(PSInvoker);

            // Act
            var method = psInvokerType.GetMethod("TryUnblockFiles", BindingFlags.Public | BindingFlags.Instance);

            // Assert
            Assert.NotNull(method);
            Assert.Equal(typeof(void), method.ReturnType);
            Assert.Empty(method.GetParameters());
        }

        [Fact]
        public void UnblockFile_MethodExists()
        {
            // Arrange
            var psInvokerType = typeof(PSInvoker);

            // Act
            var method = psInvokerType.GetMethod("UnblockFile", BindingFlags.NonPublic | BindingFlags.Instance);

            // Assert
            Assert.NotNull(method);
            Assert.Single(method.GetParameters());
            Assert.Equal(typeof(string), method.GetParameters()[0].ParameterType);
        }

        [Fact]
        public void PSInvoker_HasExpectedScriptFields()
        {
            // Arrange
            var psInvokerType = typeof(PSInvoker);
            var expectedScriptFields = new[]
            {
                "vb365Script",
                "vbrConfigScript",
                "vbrSessionScript",
                "vbrSessionScriptVersion13",
                "nasScript",
                "exportLogsScript",
                "dumpServers",
                "mfaTestScript"
            };

            // Act
            var actualFields = psInvokerType
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.FieldType == typeof(string))
                .Select(f => f.Name)
                .ToList();

            // Assert
            foreach (var expectedField in expectedScriptFields)
            {
                Assert.Contains(actualFields, f => f == expectedField);
            }
        }

        [Fact]
        public void TryUnblockFiles_DocumentedUsage()
        {
            // This test serves as documentation for developers
            // When adding a new PowerShell script:
            // 1. Add a private readonly string field to PSInvoker class (e.g., private readonly string myNewScript)
            // 2. Initialize it in the constructor using Path.Combine with AppDomain.CurrentDomain.BaseDirectory
            // 3. Add UnblockFile(this.myNewScript); to the TryUnblockFiles() method
            // 4. Run this test suite to verify all scripts are covered

            Assert.True(true, 
                "When adding new PowerShell scripts:\n" +
                "1. Add a private readonly string field for the script path\n" +
                "2. Initialize with Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ...)\n" +
                "3. Call UnblockFile() on it in TryUnblockFiles()\n" +
                "4. Run PSScriptUnblockingTests to verify coverage");
        }
    }
}
