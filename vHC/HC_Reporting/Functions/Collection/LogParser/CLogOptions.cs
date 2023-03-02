// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
namespace VeeamHealthCheck.Functions.Collection.LogParser
{
    class CLogOptions
    {
        public static readonly string VMCLOG = "\\Utils\\VMC.log";
        public static readonly string installIdLine = "InstallationId:";

        private static string _installId;

        public CLogOptions(string mode)
        {
            CVmcReader vReader = new(mode);
            vReader.PopulateVmc();
            _installId = vReader.INSTALLID;
        }

        public static string INSTALLID
        {
            get
            {
                if (!string.IsNullOrEmpty(_installId))
                    return _installId;
                else
                    return "";
            }
        }

    }
}
