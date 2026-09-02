// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Collections.Generic;

namespace VeeamHealthCheck.Functions.Collection.LogParser
{
    class CLogOptions
    {
        public static readonly string VMCLOG = "\\Utils\\VMC.log";
        public static readonly string installIdLine = "InstallationId:";

        // Keyed by the "vbr"/"vb365" mode string, so a combined install's two CLogOptions
        // instances (constructed back-to-back by CCollections.ExecVmcReader(), "vbr" first)
        // each keep their own install ID instead of sharing one static value - otherwise a
        // VB365 lookup that fails (e.g. no VMC.log present) would blank out, or silently
        // overwrite, the earlier VBR pass's ID on the VBR report.
        private static readonly Dictionary<string, string> installIdsByMode = new();

        public CLogOptions(string mode)
        {
            CVmcReader vReader = new(mode);
            vReader.PopulateVmc();

            if (!string.IsNullOrEmpty(vReader.INSTALLID))
            {
                installIdsByMode[mode] = vReader.INSTALLID;
            }
        }

        public static string GetInstallId(string mode)
        {
            return installIdsByMode.TryGetValue(mode, out string id) ? id : string.Empty;
        }
    }
}
