// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace VeeamHealthCheck.Logging
{
    public class CLogger
    {
        private string _logFile;
        public CLogger(string jobName)
        {
            CreateLogFile(jobName);
        }
        public void CreateLogFile(string jobName)
        {
            DateTime dt = DateTime.Now;
            //string currentDir = Environment.CurrentDirectory;

            CVariables cv = new();
            string currentDir = cv.OutDir;
            string logDir = Path.Combine(currentDir + "\\Log");
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            string logName = String.Format("Job.{1}_{0}_.log", dt.ToString("yyyy.MM.dd_HHmmss"), jobName);
            
            //File.Create(logName).Close();
            _logFile = logDir + "\\" + logName;
        }
        private void VerifyPath()
        {
            if (!Directory.Exists(_logFile))
            {
                Directory.CreateDirectory(_logFile);
            }
        }
        public  void LogProgress(string message)
        {
            message = FormLogLine(message, "INFO");

            Console.Write(message);
            LogLine(message, true, 3);
        }
        public  void Info(string message)
        {
            Info(message, true);
        }
        public  void Info(string message, bool silent)
        {
            message = FormLogLine(message, "INFO");
            LogLine(message, silent, 3);
        }

        private  string FormLogLine(string message, string type)
        {
            DateTime now = DateTime.Now;
            message = String.Format("[{0}]\t{2}\t{1}", now.ToString("dd.MM.yyyy HH:mm:ss"), message, type);
            return message;
        }

        public  void Warning(string message)
        {
            Warning(message, true);
        }
        public  void Warning(string message, bool silent)
        {
            message = FormLogLine(message, "WARN");
            LogLine(message, silent, 2);
        }

        public  void Error(string message)
        {
            Error(message, true);
        }
        public  void Error(string message, bool silent)
        {
            DateTime now = DateTime.Now;
            string logLine = String.Format("[{0}]\tERROR\t{1}", now.ToString("dd.MM.yyyy HH:mm:ss"), message);
            LogLine(logLine, silent, 1);
        }
        private  void LogLine(string line, bool silent, int severity)
        {
            if (!silent)
            {
                WriteToConsole(severity, line);
            }
            using (StreamWriter sw = new StreamWriter(_logFile, append: true))
            {
                sw.WriteLine(line);
            }
        }

        private  void WriteToConsole(int severity, string message)
        {
            ConsoleColor defaultColor = Console.ForegroundColor;
            switch (severity)
            {
                case 1:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(message);
                    ResetColor(defaultColor);
                    break;
                case 2:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(message);
                    ResetColor(defaultColor);
                    break;
                case 3:
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(message);
                    ResetColor(defaultColor);
                    break;
            }
        }
        private  void ResetColor(ConsoleColor color)
        {
            Console.ForegroundColor = color;
        }
    }
}
