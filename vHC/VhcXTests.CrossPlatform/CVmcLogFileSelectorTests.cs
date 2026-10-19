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
    public void SelectVmcLogFile_RotatedBackupListedBeforeLiveFile_ReturnsLiveFile()
    {
        // Arrange - Directory.GetFiles() enumeration order is filesystem-defined, not
        // chronological. Even when the rotated backup is listed first, the live "VMC.log"
        // must win deterministically over "VMC.log.1".
        string[] files = new[]
        {
            @"C:\ProgramData\Veeam\Backup365\Logs\VMC.log.1",
            @"C:\ProgramData\Veeam\Backup365\Logs\VMC.log",
        };

        // Act
        var result = CVmcLogFileSelector.SelectVmcLogFile(files);

        // Assert
        Assert.Equal(@"C:\ProgramData\Veeam\Backup365\Logs\VMC.log", result);
    }

    [Fact]
    public void SelectVmcLogFile_LiveFileListedBeforeRotatedBackup_ReturnsLiveFile()
    {
        // Arrange - same rotation scenario, opposite enumeration order, same expected result.
        string[] files = new[]
        {
            @"C:\ProgramData\Veeam\Backup365\Logs\VMC.log",
            @"C:\ProgramData\Veeam\Backup365\Logs\VMC.log.1",
        };

        // Act
        var result = CVmcLogFileSelector.SelectVmcLogFile(files);

        // Assert
        Assert.Equal(@"C:\ProgramData\Veeam\Backup365\Logs\VMC.log", result);
    }

    [Fact]
    public void SelectVmcLogFile_OnlyRotatedBackupsPresent_ReturnsFirstInCallerOrder()
    {
        // Arrange - no exact "VMC.log" exists (e.g. the live file was deleted). There is no
        // recency (LastWriteTime) signal available to this helper, so it falls back to
        // whichever rotated backup is first in the caller-supplied order - this is NOT a
        // "most recent" guarantee, just an order-preservation contract.
        string[] files = new[]
        {
            @"C:\ProgramData\Veeam\Backup365\Logs\VMC.log.2",
            @"C:\ProgramData\Veeam\Backup365\Logs\VMC.log.1",
        };

        // Act
        var result = CVmcLogFileSelector.SelectVmcLogFile(files);

        // Assert
        Assert.Equal(@"C:\ProgramData\Veeam\Backup365\Logs\VMC.log.2", result);
    }

    [Fact]
    public void SelectVmcLogFile_LowerCaseFileName_MatchesCaseInsensitively()
    {
        // Arrange - Windows filesystems are case-insensitive, so a file that legitimately
        // exists as "vmc.log" must still be recognized as the live file.
        string[] files = new[]
        {
            @"C:\ProgramData\Veeam\Backup365\Logs\Collector.log",
            @"C:\ProgramData\Veeam\Backup365\Logs\vmc.log",
        };

        // Act
        var result = CVmcLogFileSelector.SelectVmcLogFile(files);

        // Assert
        Assert.Equal(@"C:\ProgramData\Veeam\Backup365\Logs\vmc.log", result);
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
