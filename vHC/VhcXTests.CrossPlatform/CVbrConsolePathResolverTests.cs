// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using Xunit;
using VeeamHealthCheck.Functions.Collection.DB;

namespace VhcXTests.CrossPlatform;

public class CVbrConsolePathResolverTests
{
    [Fact]
    public void SiblingConsoleDir_CorePathWithTrailingBackslash_ReturnsSiblingConsoleDirectory()
    {
        string? result = CVbrConsolePathResolver.SiblingConsoleDir(@"D:\Program Files\Veeam\Backup and Replication\Backup\");

        Assert.Equal(@"D:\Program Files\Veeam\Backup and Replication\Console", result);
    }

    [Fact]
    public void SiblingConsoleDir_MountServicePathWithoutTrailingBackslash_ReturnsSiblingConsoleDirectory()
    {
        string? result = CVbrConsolePathResolver.SiblingConsoleDir(@"C:\Program Files\Common Files\Veeam\Backup and Replication\Mount Service");

        Assert.Equal(@"C:\Program Files\Common Files\Veeam\Backup and Replication\Console", result);
    }

    [Fact]
    public void SiblingConsoleDir_NullPath_ReturnsNull()
    {
        string? result = CVbrConsolePathResolver.SiblingConsoleDir(null);

        Assert.Null(result);
    }

    [Fact]
    public void SiblingConsoleDir_EmptyPath_ReturnsNull()
    {
        string? result = CVbrConsolePathResolver.SiblingConsoleDir(string.Empty);

        Assert.Null(result);
    }

    [Fact]
    public void SiblingConsoleDir_DriveRootWithNoParent_ReturnsNull()
    {
        string? result = CVbrConsolePathResolver.SiblingConsoleDir(@"D:\");

        Assert.Null(result);
    }
}
