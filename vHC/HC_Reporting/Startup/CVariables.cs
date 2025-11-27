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
        public static string vb365Dir = "\\VB365";
        public static string VbrDir = "\\VBR";
        public static string safeSuffix = @"\vHC-AnonymousReport";
        public static string unsafeSuffix = @"\vHC-Report";

        public static string desiredDir { get { return CGlobals.desiredPath; } }

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
            get { return unsafeDir + vbrDir; }
        }
    }
}
