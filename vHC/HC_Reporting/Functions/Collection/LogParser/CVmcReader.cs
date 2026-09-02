// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using VeeamHealthCheck.Functions.Collection.DB;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Collection.LogParser
{
    class CVmcReader
    {
        private string LOGLOCATION;
        public string INSTALLID;

        private readonly string mode;

        // Test seams - production never sets these, defaults preserve real behavior.
        internal string Vb365LogsDir { get; set; } = @"C:\ProgramData\Veeam\Backup365\Logs\";
        internal Action<string> WarningSink { get; set; } = CGlobals.Logger.Warning;
        internal Action<string> ErrorSink { get; set; } = CGlobals.Logger.Error;

        public CVmcReader(string mode)
        {
            this.mode = mode;
        }

        public void PopulateVmc()
        {
            try
            {
                this.GetLogDir();
                if (!string.IsNullOrEmpty(this.LOGLOCATION))
                {
                    this.ReadVmc();
                }
            }
            catch (Exception e)
            {
                this.ErrorSink(e.Message);
            }
        }

        private void GetLogDir()
        {
            if (this.mode == "vbr")
            {
                CRegReader reg = new();
                string regDir = reg.DefaultLogDir();
                this.LOGLOCATION = Path.Combine(regDir + CLogOptions.VMCLOG);
            }
            else if (this.mode == "vb365")
            {
                string[] filesList = Directory.GetFiles(this.Vb365LogsDir);
                string match = CVmcLogFileSelector.SelectVmcLogFile(filesList);
                if (match == null)
                {
                    this.WarningSink($"[VMC reader] No VMC.log file found under '{this.Vb365LogsDir}' - skipping VB365 install ID lookup.");
                    return;
                }

                this.LOGLOCATION = match;
            }
        }

        private void ReadVmc()
        {
            using (StreamReader sr = new StreamReader(this.LOGLOCATION))
            {
                string line = string.Empty;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains(CLogOptions.installIdLine))
                    {
                        this.ParseInstallId(line);
                    }
                }
            }
        }

        private void ParseInstallId(string line)
        {
            // A malformed/truncated line here must not be indistinguishable from "no VMC.log
            // found at all" - log it explicitly instead of letting it fall through to
            // PopulateVmc's generic catch, which only logs the bare exception message.
            if (line.Length <= 40)
            {
                this.WarningSink($"[VMC reader] '{CLogOptions.installIdLine}' line is shorter than expected - skipping install ID parse. Line: '{line}'");
                return;
            }

            string[] id = line.Substring(40).Split();
            if (id.Length < 2)
            {
                this.WarningSink($"[VMC reader] '{CLogOptions.installIdLine}' line did not contain an install ID token after the prefix - skipping. Line: '{line}'");
                return;
            }

            // A shifted prefix (e.g. one stray leading char) can make Split() emit a leading
            // empty entry, landing the "InstallationId:" label itself in id[1] instead of the
            // real token. The actual install ID format isn't documented anywhere in this repo
            // or the local VBR docs mirror, so don't assume a specific shape (e.g. GUID) -
            // just reject the one concrete failure mode this would otherwise produce.
            if (id[1] == CLogOptions.installIdLine || id[1].Contains(':'))
            {
                this.WarningSink($"[VMC reader] '{CLogOptions.installIdLine}' token looked like the label, not an ID - skipping. Line: '{line}'");
                return;
            }

            this.INSTALLID = id[1];
        }
    }
}
