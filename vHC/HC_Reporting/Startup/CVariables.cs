// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck
{
    public class CVariables
    {
        public static readonly string outDir = @"C:\temp\vHC";
        public static string safeDir = @"C:\temp\vHC\Anonymous";
        public static string unsafeDir = @"C:\temp\vHC\Original";
        public static string _vb365Dir = "\\VB365";
        public static string _vbrDir = "\\VBR";
        public static string _safeSuffix = @"\vHC-AnonymousReport";
        public static string _unsafeSuffix = @"\vHC-Report";
        public static string desiredDir { get { return CGlobals._desiredPath; } }
        public string unSafeDir2()
        {
            return unsafeDir;
        }
        public static string vb365dir
        {
            get
            {
                return unsafeDir + _vb365Dir;
            }
        }
        public static string vbrDir
        {
            get { return unsafeDir + _vbrDir; }
        }

    }
}
