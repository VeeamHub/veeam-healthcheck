// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License

namespace VeeamHealthCheck.Functions.Reporting.DataTypes
{
    class CProvisionTypes
    {
        public string UnderProvisioned { get { return "under"; } }
        public string OverProvisioned { get { return "over"; } }
        public string WellProvisioned { get { return " "; } }
        public CProvisionTypes()
        {

        }
    }
}
