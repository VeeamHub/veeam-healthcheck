// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
namespace VeeamHealthCheck.Functions.Collection.LogParser
{
    class CLogOptions
    {
        public static readonly string VMCLOG = "\\Utils\\VMC.log";
        public static readonly string installIdLine = "InstallationId:";

        private static string installId;

        public CLogOptions(string mode)
        {
            CVmcReader vReader = new(mode);
            vReader.PopulateVmc();

            // installId is static and shared across the "vbr" and "vb365" instances that
            // CCollections.ExecVmcReader() constructs back-to-back on a combined install -
            // only overwrite it when this pass actually found one, so a failed/skipped
            // lookup (e.g. no VB365 VMC.log present) can't blank out a value the other
            // pass already found.
            if (!string.IsNullOrEmpty(vReader.INSTALLID))
            {
                installId = vReader.INSTALLID;
            }
        }

        public static string INSTALLID
        {
            get
            {
                if (!string.IsNullOrEmpty(installId))
                {
                    return installId;
                }
                else
                {

                    return string.Empty;
                }
            }
        }
    }
}
