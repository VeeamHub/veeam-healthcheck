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

        private readonly VmcLogMode mode;

        // Test seams - production never sets these, defaults preserve real behavior.
        internal string Vb365LogsDir { get; set; } = @"C:\ProgramData\Veeam\Backup365\Logs\";
        internal Action<string> WarningSink { get; set; } = msg => CGlobals.Logger.Warning(msg);
        internal Action<string> ErrorSink { get; set; } = msg => CGlobals.Logger.Error(msg);

        public CVmcReader(VmcLogMode mode)
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
            if (this.mode == VmcLogMode.Vbr)
            {
                CRegReader reg = new();
                string regDir = reg.DefaultLogDir();
                this.LOGLOCATION = Path.Combine(regDir + CLogOptions.VMCLOG);
            }
            else
            {
                string[] filesList = Directory.GetFiles(this.Vb365LogsDir, "*VMC.log*");
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

            // Locate the label token itself rather than assuming it lands at a fixed offset:
            // this tolerates any amount of drift in the assumed 40-char prefix width (which
            // isn't documented anywhere in this repo or the local VBR docs mirror) instead of
            // silently mis-parsing a shifted line. The install ID's own format is likewise
            // unverified, so don't assume a shape for it either - just take whatever token
            // immediately follows the label.
            string[] tokens = line.Substring(40).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            int labelIndex = Array.IndexOf(tokens, CLogOptions.installIdLine);
            if (labelIndex < 0 || labelIndex + 1 >= tokens.Length)
            {
                this.WarningSink($"[VMC reader] '{CLogOptions.installIdLine}' line did not contain an install ID token after the label - skipping. Line: '{line}'");
                return;
            }

            this.INSTALLID = tokens[labelIndex + 1];
        }
    }
}
