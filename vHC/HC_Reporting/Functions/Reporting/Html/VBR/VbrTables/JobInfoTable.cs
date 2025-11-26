using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables
{
    public class JobInfoTable
    {
        public List<JobInfo> Jobs { get; set; }

    }

    public class JobInfo
    {
        public string Name { get; set; }

        public string Repository { get; set; }

        // create members for each of these: Name	Repository	Source Size	Retention Scheme	Restore Points	Encrypted	Job Type	Compression Level	Block Size	GFS Enabled	GFS Retention	Active Full Enabled	Synthetic Full Enabled	Backup Chain Type	Indexing Enabled
        public string SourceSize { get; set; }

        public string RetentionScheme { get; set; }

        public string RestorePoints { get; set; }

        public string Encrypted { get; set; }

        public string JobType { get; set; }

        public string CompressionLevel { get; set; }

        public string BlockSize { get; set; }

        public string GfsEnabled { get; set; }

        public string GfsRetention { get; set; }

        public string ActiveFullEnabled { get; set; }

        public string SyntheticFullEnabled { get; set; }

        public string BackupChainType { get; set; }

        public string IndexingEnabled { get; set; }
    }
}
