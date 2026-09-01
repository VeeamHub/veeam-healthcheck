// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
// Cross-platform tests for CVmcLogFileSelector (Issue: VB365 VMC reader NullReferenceException)

using VeeamHealthCheck.Functions.Collection.LogParser;
using Xunit;

namespace VhcXTests.CrossPlatform;

public class CVmcLogFileSelectorTests
{
    [Fact]
    public void SelectVmcLogFile_NoFilesInDirectory_ReturnsNull()
    {
        // Arrange - the exact condition that crashed CVmcReader.GetLogDir():
        // Directory.GetFiles() returned an empty array (no files at all).
        string[] files = System.Array.Empty<string>();

        // Act
        var result = CVmcLogFileSelector.SelectVmcLogFile(files);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SelectVmcLogFile_NoFileNameContainsVmcLog_ReturnsNull()
    {
        // Arrange - files exist, but none of them is a VMC.log.
        string[] files = new[]
        {
            @"C:\ProgramData\Veeam\Backup365\Logs\Collector.log",
            @"C:\ProgramData\Veeam\Backup365\Logs\Svc.Archiver.log",
        };

        // Act
        var result = CVmcLogFileSelector.SelectVmcLogFile(files);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SelectVmcLogFile_SingleMatchingFile_ReturnsThatFile()
    {
        // Arrange
        string[] files = new[]
        {
            @"C:\ProgramData\Veeam\Backup365\Logs\Collector.log",
            @"C:\ProgramData\Veeam\Backup365\Logs\VMC.log",
        };

        // Act
        var result = CVmcLogFileSelector.SelectVmcLogFile(files);

        // Assert
        Assert.Equal(@"C:\ProgramData\Veeam\Backup365\Logs\VMC.log", result);
    }

    [Fact]
    public void SelectVmcLogFile_MultipleMatchingFiles_ReturnsFirstMatch()
    {
        // Arrange - preserves the original (pre-fix) selection order: first match
        // in the directory listing, not a specific sort order.
        string[] files = new[]
        {
            @"C:\ProgramData\Veeam\Backup365\Logs\VMC.log.1",
            @"C:\ProgramData\Veeam\Backup365\Logs\VMC.log",
        };

        // Act
        var result = CVmcLogFileSelector.SelectVmcLogFile(files);

        // Assert
        Assert.Equal(@"C:\ProgramData\Veeam\Backup365\Logs\VMC.log.1", result);
    }

    [Fact]
    public void SelectVmcLogFile_NullInput_ReturnsNull()
    {
        // Act
        var result = CVmcLogFileSelector.SelectVmcLogFile(null);

        // Assert
        Assert.Null(result);
    }
}
