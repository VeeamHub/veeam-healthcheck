// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck
{
    public class CVariables
    {
        public static readonly string outDir = @"C:\temp\vHC";
        
        // Use CGlobals.desiredPath as the base, falling back to the default if not set
        public static string safeDir => Path.Combine(CGlobals.desiredPath ?? outDir, "Anonymous");
        public static string unsafeDir => Path.Combine(CGlobals.desiredPath ?? outDir, "Original");
        
        public static string vb365Dir = "\\VB365";
        public static string VbrDir = "\\VBR";
        public static string safeSuffix = @"\vHC-AnonymousReport";
        public static string unsafeSuffix = @"\vHC-Report";

        public string unSafeDir2()
        {
            return unsafeDir;
        }

        public static string vb365dir
        {
            get
            {
                return unsafeDir + vb365Dir;
            }
        }

        public static string vbrDir
        {
            get { return GetVbrDirWithTimestamp(); }
        }

        /// <summary>
        /// Gets the VBR directory path with server name and timestamp subdirectories.
        /// Format: C:\temp\vHC\Original\VBR\{servername}\{timestamp}
        /// </summary>
        private static string GetVbrDirWithTimestamp()
        {
            string basePath = unsafeDir + VbrDir;
            
            // Get server name - use VBRServerName if set, otherwise default to "localhost"
            string serverName = string.IsNullOrEmpty(CGlobals.VBRServerName) ? "localhost" : CGlobals.VBRServerName;
            
            // Get or create timestamp for this run
            string timestamp = CGlobals.GetRunTimestamp();
            
            // Build path: base\servername\timestamp
            string fullPath = Path.Combine(basePath, serverName, timestamp);
            
            return fullPath;
        }

        /// <summary>
        /// Gets the base VBR directory without server/timestamp subdirectories.
        /// Used for compatibility and when needed to access the base path.
        /// </summary>
        public static string GetVbrBaseDir()
        {
            return unsafeDir + VbrDir;
        }
    }
}
