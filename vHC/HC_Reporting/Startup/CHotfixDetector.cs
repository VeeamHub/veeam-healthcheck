// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using VeeamHealthCheck.Functions.Collection;
using VeeamHealthCheck.Functions.Collection.LogParser;
using VeeamHealthCheck.Functions.Collection.PSCollections;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Startup
{
    internal class CHotfixDetector
    {
        private readonly CLogger LOG = CGlobals.Logger;
        private readonly string logStart = "[HotfixDetector]\t";
        private readonly string originalPath;
        private string path;
        private readonly List<string> fixList;

        public CHotfixDetector(string path)
        {
            this.fixList = new List<string>();
            CClientFunctions funk = new();
            if (funk.VerifyPath(path))
            {
                this.originalPath = path;
                this.SetPath();
            }
        }

        public void Run()
        {
            CCollections col = new();
            this.LOG.Info(this.logStart + "Checking Path...", false);
            this.ExecLogCollection();

            // TryParseRegLogs();
            this.EchoResults();
        }

        private void EchoResults()
        {
            if (this.fixList.Count > 0)
            {
                this.LOG.Warning(this.logStart + CMessages.FoundHotfixesMessage(this.fixList), false);
            }
            else
                this.LOG.Warning(this.logStart + "No hotfixes found.", false);
        }

        private void SetPath()
        {
            this.path = this.originalPath + "\\vHC_HotFixDetectionLogs";
            if (!Directory.Exists(this.path))
            {
                Directory.CreateDirectory(this.path);
            }
        }

        private void TryParseLogs()
        {
            try
            {
                string output = this.ExtractLogs();
                this.EnumerateFiles(output);
                this.ClearTargetPath(output);
            }
            catch (Exception ex) { this.LOG.Error(this.logStart + ex.Message, false); }
        }

        private void TryParseRegLogs()
        {
            try
            {
                this.ParseRegularLogs();
            }
            catch (Exception e2)
            {
                this.LOG.Error(this.logStart + e2.Message, false);
            }
        }

        private void EnumerateFiles(string path)
        {
            string[] files = Directory.GetFiles(path, "*.log", searchOption: SearchOption.AllDirectories);
            int counter = 1;
            foreach (string file in files)
            {
                try
                {
                    this.LOG.Info(this.logStart + "Checking file " + counter + " of " + files.Count(), false);
                    this.Parse(file);
                    counter++;
                }
                catch (Exception e)
                {
                    // LOG.Error(logStart + e.Message, false);
                }
            }
        }

        private bool VerifyPath(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return false;
            }


            if (path.StartsWith("\\\\"))
            {
                return false;
            }


            if (Directory.Exists(path))
            {
                return true;
            }


            if (this.TryCreateDir(path))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool TryCreateDir(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch
            {
                this.LOG.Error("Failed to create directory.", false);
                return false;
            }
        }

        private void ExecLogCollection()
        {
            PSInvoker ps = new();
            ps.RunServerDump();

            // get file + results
            foreach(string server in this.ServerList())
            {
                this.LOG.Info("Checking logs for: " + server, false);
                ps.RunVbrLogCollect(this.path, server);
                this.TryParseLogs();
            }
        }

        private List<string> ServerList()
        {
            List<string> newList = new();
            string dir = Directory.GetCurrentDirectory();
            string path = /*dir +*/ PSInvoker.SERVERLISTFILE;
            using(StreamReader sr = new(path)) // need to get the source directory
            {
                string line;
                while((line = sr.ReadLine()) != null)
                {
                    newList.Add(line);
                }
            }

            return newList.Distinct().ToList();
        }

        private string ExtractLogs()
        {
            string target = this.path + "\\extracted";
            this.ClearTargetPath(target);
            var files = Directory.GetFiles(this.path + "\\VeeamSupportLogs");
            foreach (string file in files)
            {
                using (ZipArchive zip = ZipFile.OpenRead(file))
                {
                    try
                    {
                        zip.ExtractToDirectory(target);
                    }
                    catch (Exception e) { }
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
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception e) { }
                string[] dirs = Directory.GetDirectories(path);
                foreach (string dir in dirs)
                {
                    this.ClearTargetPath(dir);
                    try
                    {
                        Directory.Delete(path, true);
                    }
                    catch (Exception e) { } 
                }
            }
        }

        private void ParseRegularLogs()
        {
            CLogParser logParser = new CLogParser();
            string path = logParser.InitLogDir();
            this.EnumerateFiles(path);
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
                        {
                            this.ParseFixLines(line);
                        }
                    }
                }
            }
        }

        private void ParseFixLines(string line)
        {
            try
            {
                string fileVersion = line.Remove(0, line.IndexOf("File Version"));

                // log.Debug(line, false);
                string fixLine = line.Remove(0, line.IndexOf("Private Fix"));
                if (fixLine.EndsWith(']'))
                {
                    fixLine = fixLine.Replace("]", string.Empty);
                }


                if (!this.fixList.Contains(fixLine))
                {
                    if (!fixLine.Contains("KB"))
                    {
                        this.fixList.Add(fixLine);
                        this.LOG.Debug(fixLine, false);
                    }
                }
            }
            catch (Exception e) { this.LOG.Debug(e.Message); }
        }
    }
}
