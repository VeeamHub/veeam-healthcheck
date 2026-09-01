// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace VeeamHealthCheck.Functions.Collection.LogParser
{
    internal static class CVmcLogFileSelector
    {
        internal static string? SelectVmcLogFile(IEnumerable<string>? filePaths)
        {
            return filePaths?.FirstOrDefault(f => f.Contains("VMC.log"));
        }
    }
}
