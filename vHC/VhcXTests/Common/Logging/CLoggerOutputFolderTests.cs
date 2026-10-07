// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;
using Xunit;

namespace VhcXTests.Common.Logging
{
    [Collection("GlobalState")]
    public class CLoggerOutputFolderTests : IDisposable
    {
        private readonly string originalDesiredPath = CGlobals.desiredPath;
        private readonly CLogger originalMainLog = CGlobals.mainlog;
        private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "vHC_LoggerTest_" + Guid.NewGuid());

        public CLoggerOutputFolderTests()
        {
            Directory.CreateDirectory(this.tempRoot);
        }

        public void Dispose()
        {
            CGlobals.desiredPath = this.originalDesiredPath;
            CGlobals.mainlog = this.originalMainLog;
            try
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(this.tempRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }

                Directory.Delete(this.tempRoot, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }

        [Fact]
        public void RelocateMainLog_AfterDesiredPathChange_PointsLogFileAtNewPath()
        {
            string outDir = Path.Combine(this.tempRoot, "custom");
            CGlobals.desiredPath = outDir;

            CGlobals.RelocateMainLog();

            Assert.Contains(outDir, CGlobals.mainlog.logFile, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RelocateMainLog_AfterDesiredPathChange_WritesLogLineToNewFile()
        {
            CGlobals.desiredPath = Path.Combine(this.tempRoot, "custom");
            CGlobals.RelocateMainLog();

            CGlobals.Logger.Info("relocation probe", true);

            Assert.True(File.Exists(CGlobals.mainlog.logFile));
            Assert.Contains("relocation probe", File.ReadAllText(CGlobals.mainlog.logFile));
        }

        [Fact]
        public void Constructor_OutputFolderCannotBeCreated_DoesNotThrow()
        {
            // A regular file where a parent directory is needed makes CreateDirectory fail
            // on every platform.
            string blocker = Path.Combine(this.tempRoot, "blocker");
            File.WriteAllText(blocker, string.Empty);
            CGlobals.desiredPath = Path.Combine(blocker, "out");

            var ex = Record.Exception(() => new CLogger("HealthCheck"));

            Assert.Null(ex);
        }

        [Fact]
        public void Info_LogFileNotWritable_DoesNotThrow()
        {
            // Directory permission bits are only meaningful off Windows; running as root
            // bypasses them. The constructor case above covers Windows.
            if (OperatingSystem.IsWindows() || Environment.UserName == "root")
            {
                return;
            }

            CGlobals.desiredPath = this.tempRoot;
            var logger = new CLogger("HealthCheck");
            File.SetUnixFileMode(this.tempRoot, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            var ex = Record.Exception(() => logger.Info("cannot be written", true));

            Assert.Null(ex);
        }
    }
}
