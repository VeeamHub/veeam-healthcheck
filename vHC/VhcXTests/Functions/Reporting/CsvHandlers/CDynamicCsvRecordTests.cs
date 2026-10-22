using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;

namespace VhcXTests.Functions.Reporting.CsvHandlers
{
    /// <summary>
    /// _nasBackup.csv columns after Name are optional: a job whose repository is gone has no
    /// size, and a CSV from an older or partial collector can lack OnDiskGB/SourceGB entirely.
    /// Reading a missing column through the dynamic binder throws, which used to end Job Info
    /// ("FastDynamicObject does not contain a definition for 'ondiskgb'"). Report generation
    /// must show unknown as 0 instead.
    /// </summary>
    public class CDynamicCsvRecordTests
    {
        private static List<dynamic> Read(string csvText)
        {
            using var reader = new StringReader(csvText);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<dynamic>().ToList();
        }

        [Fact]
        public void GetDouble_ColumnMissingFromTheFile_ReturnsZeroInsteadOfThrowing()
        {
            var rows = Read("Name,JobType\nnas1,NAS Backup\n");

            // The old code path: the dynamic binder throws for a column that is not there.
            Assert.ThrowsAny<System.Exception>(() => { string unused = (string)rows[0].ondiskgb; });

            Assert.Equal(0, CDynamicCsvRecord.GetDouble((object)rows[0], "ondiskgb"));
            Assert.Equal(0, CDynamicCsvRecord.GetDouble((object)rows[0], "sourcegb"));
        }

        [Fact]
        public void GetDouble_ColumnPresentButBlank_ReturnsZero()
        {
            var rows = Read("Name,OnDiskGB,SourceGB\nnas1,,\n");

            Assert.Equal(0, CDynamicCsvRecord.GetDouble((object)rows[0], "ondiskgb"));
            Assert.Equal(0, CDynamicCsvRecord.GetDouble((object)rows[0], "sourcegb"));
        }

        [Fact]
        public void GetDouble_ColumnPresent_ReadsItCaseInsensitively()
        {
            var rows = Read("Name,OnDiskGB,SourceGB\nnas1,12,34\n");

            Assert.Equal(12, CDynamicCsvRecord.GetDouble((object)rows[0], "ondiskgb"));
            Assert.Equal(34, CDynamicCsvRecord.GetDouble((object)rows[0], "SOURCEGB"));
        }

        [Theory]
        [InlineData("256.75", 256.75)]
        [InlineData("1024.50", 1024.5)]
        [InlineData("not-a-number", 0)]
        public void GetDouble_GermanCurrentCulture_ParsesInvariant(string raw, double expected)
        {
            var rows = Read("Name,OnDiskGB\nnas1," + raw + "\n");
            CultureInfo previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            try
            {
                Assert.Equal(expected, CDynamicCsvRecord.GetDouble((object)rows[0], "ondiskgb"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void GetString_NullRecordOrNonRecord_ReturnsNull()
        {
            Assert.Null(CDynamicCsvRecord.GetString(null, "name"));
            Assert.Null(CDynamicCsvRecord.GetString("not a record", "name"));
            Assert.Equal(0, CDynamicCsvRecord.GetDouble(null, "ondiskgb"));
        }

        [Fact]
        public void GetString_ExpandoRecord_ReadsColumnsByName()
        {
            dynamic record = new ExpandoObject();
            record.Name = "nas1";

            Assert.Equal("nas1", CDynamicCsvRecord.GetString((object)record, "name"));
            Assert.Null(CDynamicCsvRecord.GetString((object)record, "ondiskgb"));
        }

        [Fact]
        public void GetString_NameColumnMissing_ReturnsNull()
        {
            var rows = Read("JobType\nNAS Backup\n");

            Assert.Null(CDynamicCsvRecord.GetString((object)rows[0], "name"));
        }
    }
}
