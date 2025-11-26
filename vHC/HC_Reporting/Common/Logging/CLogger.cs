// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;

namespace VeeamHealthCheck.Shared.Logging
{
    public class CLogger
    {
        public readonly string _logFile;

        public CLogger(string jobName)
        {
            _logFile = CreateLogFile(jobName);
        }

        public string CreateLogFile(string jobName)
        {
            DateTime dt = DateTime.Now;
            //string currentDir = Environment.CurrentDirectory;

            string currentDir = CVariables.unsafeDir;
            string logDir = Path.Combine(currentDir + "\\Log");
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            string logName = String.Format("Job.{1}_{0}_.log", dt.ToString("yyyy.MM.dd_HHmmss"), jobName);

            //File.Create(logName).Close();
            return logDir + "\\" + logName;
        }

        public void Info(string message)
        {
            Info(message, false);
        }

        public void Info(string message, bool silent)
        {
            message = FormLogLine(message, "INFO");
            LogLine(message, silent, 3);
        }

        private string FormLogLine(string message, string type)
        {
            DateTime now = DateTime.Now;
            message = String.Format("[{0}]\t{2}\t{1}", now.ToString("dd.MM.yyyy HH:mm:ss"), message, type);
            return message;
        }

        public void Warning(string message)
        {
            Warning(message, false);
        }

        public void Warning(string message, bool silent)
        {
            message = FormLogLine(message, "WARN");
            LogLine(message, silent, 2);
        }

        public void Debug(string message)
        {
            Debug(message, false);
        }

        public void Debug(string message, bool silent)
        {
            message = FormLogLine(message, "DEBUG");
            if (CGlobals.DEBUG)
            {
                LogLine(message, false, 2);

            }
            else
            {
                LogLine(message, true, 2);
            }
        }

        public void Error(string message)
        {

            Error(message, false);
        }

        public void Error(string message, bool silent)
        {
            DateTime now = DateTime.Now;
            string logLine = String.Format("[{0}]\tERROR\t{1}", now.ToString("dd.MM.yyyy HH:mm:ss"), message);
            LogLine(logLine, silent, 1);
        }

        private void LogLine(string line, bool silent, int severity)
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

        private void WriteToConsole(int severity, string message)
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

        private void ResetColor(ConsoleColor color)
        {
            Console.ForegroundColor = color;
        }
    }
}
