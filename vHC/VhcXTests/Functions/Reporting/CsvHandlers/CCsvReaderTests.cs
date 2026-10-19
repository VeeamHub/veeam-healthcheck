using System;
using System.IO;
using System.Linq;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using Xunit;

namespace VhcXTests.Functions.Reporting.CsvHandlers
{
    [Collection("GlobalState")]
    public sealed class CCsvReaderTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "VhcCsvReader_" + Guid.NewGuid().ToString("N"));
        private readonly string csv;

        public CCsvReaderTests()
        {
            Directory.CreateDirectory(this.root);
            this.csv = Path.Combine(this.root, "host_Servers.csv");
            File.WriteAllText(this.csv, "Name,Type\nPROXY01,Proxy\nREPO01,Repo\n");
        }

        public void Dispose() => Directory.Delete(this.root, recursive: true);

        [Fact]
        public void FileFinder_AfterReturningReader_FileHandleIsNotHeld()
        {
            using var reader = new CCsvReader().FileFinder("Servers", this.root);

            Assert.NotNull(reader);
            using var exclusive = new FileStream(this.csv, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }

        [Fact]
        public void FileFinder_ReadAfterFileDeleted_StillEnumeratesAllRecords()
        {
            using var reader = new CCsvReader().FileFinder("Servers", this.root);
            Assert.NotNull(reader);
            File.Delete(this.csv);

            var names = reader.GetRecords<dynamic>().Select(r => (string)r.name).ToList();

            Assert.Equal(new[] { "PROXY01", "REPO01" }, names);
        }

        [Fact]
        public void FileFinder_MissingToken_ReturnsNull()
        {
            Assert.Null(new CCsvReader().FileFinder("Nope", this.root));
        }
    }
}
