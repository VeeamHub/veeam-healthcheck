// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Collections.Generic;

namespace VeeamHealthCheck.Functions.Collection.LogParser
{
    class CLogOptions
    {
        public static readonly string VMCLOG = "\\Utils\\VMC.log";
        public static readonly string installIdLine = "InstallationId:";

        // Keyed by VmcLogMode, so a combined install's two CLogOptions instances (constructed
        // back-to-back by CCollections.ExecVmcReader(), Vbr first) each keep their own install
        // ID instead of sharing one static value - otherwise a VB365 lookup that fails (e.g.
        // no VMC.log present) would blank out, or silently overwrite, the earlier VBR pass's ID
        // on the VBR report.
        private static readonly Dictionary<VmcLogMode, string> installIdsByMode = new();

        public CLogOptions(VmcLogMode mode)
        {
            CVmcReader vReader = new(mode);
            vReader.PopulateVmc();

            // Always assign, even when the lookup found nothing: installIdsByMode is a
            // static dictionary that outlives a single run (e.g. a GUI retry constructs a
            // fresh CLogOptions in the same process). Skipping the write on empty would let
            // a failed retry silently keep a stale ID from the previous run instead of
            // correctly reporting "no ID this run".
            installIdsByMode[mode] = vReader.INSTALLID ?? string.Empty;
        }

        public static string GetInstallId(VmcLogMode mode)
        {
            return installIdsByMode.TryGetValue(mode, out string id) ? id : string.Empty;
        }
    }
}
