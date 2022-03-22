// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HC_Reporting.DataTypes
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
