// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Logging;

namespace VeeamHealthCheck.FilesParser
{
    class CLogParser
    {
        private CLogger log = HC_Reporting.MainWindow.log;
        private string LogLocation;
        private Dictionary<string, List<TimeSpan>> _waits = new();
        private string _pathToCsv = @"C:\temp\vHC\Raw_Data\waits.csv";

        public CLogParser()
        {
            InitLogDir();
            InitWaitCsv();
            /*check each job for long wait times
             * need:
             * - job name
             * - how long it waited
             * -- last wait
             * -- times waited
             * -- avg wait?
             * - overlaps?
             * 
             */

        }

        private void InitLogDir()
        {
            DB.CRegReader reg = new();
            LogLocation = reg.DefaultLogDir();
        }
        private void InitWaitCsv()
        {
            using (StreamWriter sw = new StreamWriter(_pathToCsv, append: false))
            {
                sw.WriteLine("JobName,StartTime,EndTime,Duration");
            }
        }
        private void DumpWaitsToFile(string JobName, DateTime start, DateTime end, TimeSpan diff)
        {

            using (StreamWriter sw = new StreamWriter(_pathToCsv, append: true))
            {
                sw.WriteLine(JobName + ","+ start + "," +end + "," +diff);
            }

            //String csv = String.Join(
            //    Environment.NewLine,
            //    _waits.Select(d => $"{d.Key};{d.Value};")
            //        );
            //System.IO.File.WriteAllText(pathToCsv, csv);
        }
        public Dictionary<string, List<TimeSpan>> GetWaitsFromFiles()
        {
            Dictionary<string, List<TimeSpan>> jobsAndWaits = new();
            string[] dirList = Directory.GetDirectories(LogLocation);
            foreach (var d in dirList)
            {
                string jobname = Path.GetFileName(d);

                List<TimeSpan> waits = new();

                string[] fileList = Directory.GetFiles(d, "Job.*.log", SearchOption.AllDirectories);

                foreach (var f in fileList)
                {
                    waits.AddRange(CheckFileWait(f, jobname));
                }
                jobsAndWaits.Add(jobname, waits);
            }
            _waits = jobsAndWaits;
            //DumpWaitsToFile();
            return jobsAndWaits;
        }
        private List<TimeSpan> CheckFileWait(string file, string jobName)
        {
            List<TimeSpan> diffListMin = new();

            using (StreamReader sr = new(file))
            {
                string line;
                string waitLine = "Waiting for backup infrastructure resources availability";
                string startTime = null;
                string endTime = null;

                bool countNextLine = false;
                while ((line = sr.ReadLine()) != null)
                {
                    if (!String.IsNullOrEmpty(line))
                    {
                        try
                        {
                            if (countNextLine)
                            {
                                endTime = line.Remove(21);

                                countNextLine = false;

                            }

                            string trimline = line.Substring(40);
                            if (trimline == waitLine)
                            {
                                startTime = line.Remove(21);
                                countNextLine = true;
                            }
                            if (!String.IsNullOrEmpty(startTime) && !String.IsNullOrEmpty(endTime))
                            {
                                diffListMin.Add(CalcTime(jobName, startTime, endTime));
                                endTime = "";
                                startTime = "";
                            }
                        }
                        catch (Exception e) { }
                    }

                }
            }
            return diffListMin;

        }

        private TimeSpan CalcTime(string jobName, string start, string end)
        {
            start = start.Trim('[');
            start = start.Trim(']');
            end = end.Trim('[');
            end = end.Trim(']');


            DateTime.TryParse(start, out DateTime tStart);
            DateTime.TryParse(end, out DateTime tEnd);


            var diffTime = (tEnd - tStart);
            //string t = diffTime.ToString("dd:HH:mm:ss");
            DumpWaitsToFile(jobName, tStart, tEnd, diffTime);
            return diffTime;
        }

    }
}
