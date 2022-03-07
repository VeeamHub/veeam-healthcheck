// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck
{
    class CVariables
    {
        public readonly string OutDir = @"C:\temp\vHC";
        public static string safeDir = @"C:\temp\vHC\Scrubbed";
        public static string unsafeDir = @"C:\temp\vHC\Contains_Sensitive_Data";
        public static string desiredDir { get; set; }
        public  string unSafeDir2()
        {
            return unsafeDir;
        }
        
    }
}
