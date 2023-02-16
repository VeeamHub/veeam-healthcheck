using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Collection;
using VeeamHealthCheck.FilesParser;
using VeeamHealthCheck.Html.VBR;
using VeeamHealthCheck.Reporting.CsvHandlers.VB365;
using VeeamHealthCheck.Reporting.Html;
using VeeamHealthCheck.Reporting.TableDatas;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Startup
{
    internal class CHotfixDetector
    {
        private CLogger LOG = CGlobals.Logger;
        private string logStart = "[HotfixDetector]\t";
        private readonly string _originalPath;
        private string _path;
        private List<string> _fixList;

        private string _vbrVersion;

        public CHotfixDetector(string path)
        {
            _fixList = new List<string>();
            if (VerifyPath(path))
            {
                _originalPath = path;
                SetPath();
            }
        }
        public void Run()
        {
            CCollections col = new();
            col.RunVbrConfigOnly();
            LOG.Info(logStart + "Checking Path...", false);
            ExecLogCollection();
            ParseLogs();
            GetVbrServerVersion();
            EchoResults(_vbrVersion);
        }
        private void GetVbrServerVersion()
        {
            CDataFormer df = new(false);
            BackupServer b = df.BackupServerInfoToXml(false);
            _vbrVersion = b.Version;

        }
        private void EchoResults(string vbrVersion)
        {
            if (_fixList.Count > 0)
            {
                LOG.Warning(logStart + CMessages.FoundHotfixesMessage(_fixList, _vbrVersion), false);
            }
            else
                LOG.Warning(logStart + "No hotfixes found.", false);
        }
        private void SetPath()
        {
            _path = _originalPath + "\\vHC_HotFixDetectionLogs";
            if (!Directory.Exists(_path))
            {
                Directory.CreateDirectory(_path);
            }
        }
        private void ParseLogs()
        {
            try
            {
                string output = ExtractLogs();
                EnumerateFiles(output);
                ClearTargetPath(output);
            }
            catch (Exception ex) { LOG.Error(logStart + ex.Message, false); }

            try
            {
                ParseRegularLogs();
            }
            catch (Exception e2)
            {
                LOG.Error(logStart + e2.Message, false);
            }
        }
        private void EnumerateFiles(string path)
        {
            string[] files = Directory.GetFiles(path, "*.log", searchOption: SearchOption.AllDirectories);
            foreach (string file in files)
            {
                try
                {
                    Parse(file);

                }
                catch (Exception e)
                {
                    //LOG.Error(logStart + e.Message, false);
                }
            }
        }
        private bool VerifyPath(string path)
        {
            if (String.IsNullOrEmpty(path)) return false;
            if (Directory.Exists(path)) return true;
            else return false;
        }
        private void ExecLogCollection()
        {
            PSInvoker ps = new();
            ps.RunVbrLogCollect(_path);
        }
        private string ExtractLogs()
        {
            string target = _path + "\\extracted";
            ClearTargetPath(target);
            var files = Directory.GetFiles(_path + "\\VeeamSupportLogs");
            foreach (string file in files)
            {
                using (ZipArchive zip = ZipFile.OpenRead(file))
                {
                    zip.ExtractToDirectory(target);

                }
                File.Delete(file);
            }
            return target;
        }
        private void ClearTargetPath(string path)
        {
            if (Directory.Exists(path))
            {
                string[] files = Directory.GetFiles(path);
                foreach (string file in files)
                    File.Delete(file);
                string[] dirs = Directory.GetDirectories(path);
                foreach (string dir in dirs)
                {
                    ClearTargetPath(dir);
                    Directory.Delete(path, true);

                }


            }
        }
        private void ParseRegularLogs()
        {
            CLogParser logParser = new CLogParser();
            string path = logParser.InitLogDir();
            EnumerateFiles(path);
        }
        private void Parse(string file)
        {
            using (StreamReader sr = new(file))
            {
                string line;

                while ((line = sr.ReadLine()) != null)
                {
                    if (!String.IsNullOrEmpty(line))
                    {
                        if (line.Contains("Private Fix"))
                            ParseFixLines(line);
                    }

                }
            }
        }
        private void ParseFixLines(string line)
        {
            try
            {

                //log.Debug(line, false);
                string fixLine = line.Remove(0, line.IndexOf("Private Fix"));
                if (fixLine.EndsWith(']'))
                    fixLine = fixLine.Replace("]", "");
                if (!_fixList.Contains(fixLine))
                {
                    if (!fixLine.Contains("KB"))
                    {
                        _fixList.Add(fixLine);
                        LOG.Debug(fixLine, false);
                    }


                }
            }
            catch (Exception e) { LOG.Debug(e.Message); }
        }
    }
}
