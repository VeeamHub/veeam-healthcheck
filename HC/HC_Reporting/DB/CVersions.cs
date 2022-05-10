// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.DB
{
    class CVersions
    {
        private string _vbrVersion;
        public string VbrVersion { get { return _vbrVersion; } }
        public CVersions(int dbVersion)
        {
            SetVbrVersion(dbVersion);
        }

        private void SetVbrVersion(int dbVersion)
        {
            foreach (var v in VbrVersions())
            {
                if (dbVersion == v.Value)
                    _vbrVersion = v.Key;
            }
        }
        private Dictionary<string, int> VbrVersions()
        {
            Dictionary<string, int> d = new();

            d.Add("6.0.0.153", 455);
            d.Add("6.0.0.158", 456);
            d.Add("6.0.0.164", 457);
            d.Add("6.0.0.181", 458);
            d.Add("6.0.0.201", 459);


            d.Add("6.1.0.181", 520);
            d.Add("6.1.0.203", 523);
            d.Add("6.1.0.204", 523);
            d.Add("6.1.0.205", 523);
            d.Add("6.1.0.207", 523);

            d.Add("6.5.0.106", 633);
            d.Add("6.5.0.109", 633);
            d.Add("6.5.0.128", 634);
            d.Add("6.5.0.133", 634);
            d.Add("6.5.0.144", 638);

            d.Add("7.0.0.521", 1094);
            d.Add("7.0.0.663", 1167);
            d.Add("7.0.0.690", 1179);
            d.Add("7.0.0.715", 1181);
            d.Add("7.0.0.764", 1196);
            d.Add("7.0.0.771", 1196);
            d.Add("7.0.0.833", 1199);
            d.Add("7.0.0.839", 1199);
            d.Add("7.0.0.870", 1205);
            d.Add("7.0.0.871", 1205);


            d.Add("8.0.0.266", 1563);
            d.Add("8.0.0.267", 1563);
            d.Add("8.0.0.427", 1656);
            d.Add("8.0.0.592", 1745);
            d.Add("8.0.0.754", 1858);
            d.Add("8.0.0.807", 1870);
            d.Add("8.0.0.817", 1870);
            d.Add("8.0.0.831", 1870);
            d.Add("8.0.0.917", 1872);
            d.Add("8.0.0.1274", 1991);
            d.Add("8.0.0.2018", 2016);
            d.Add("8.0.0.2021", 2017);
            d.Add("8.0.0.2029", 2018);
            d.Add("8.0.0.2030", 2018);
            d.Add("8.0.0.2084", 2022);


            d.Add("9.0.0.285", 2351);
            d.Add("9.0.0.293", 2351);
            d.Add("9.0.0.557", 2649);
            d.Add("9.0.0.560", 2649);
            d.Add("9.0.0.773", 2750);
            d.Add("9.0.0.902", 2754);
            d.Add("9.0.0.1483", 2773);
            d.Add("9.0.0.1491", 2773);
            d.Add("9.0.0.1715", 2791);


            d.Add("9.5.0.221", 3232);
            d.Add("9.5.0.348", 3379);
            d.Add("9.5.0.580", 3530);
            d.Add("9.5.0.711", 3539);
            d.Add("9.5.0.802", 3627);
            d.Add("9.5.0.823", 3628);
            d.Add("9.5.0.3029", 3628);
            d.Add("9.5.0.1038", 3701);
            d.Add("9.5.0.1046", 3701);
            d.Add("9.5.0.3045", 3701);
            d.Add("9.5.0.1335", 4033);
            d.Add("9.5.0.1536", 4051);
            d.Add("9.5.0.1922", 4087);
            d.Add("9.5.4.2176", 5006);
            d.Add("9.5.4.2282", 5170);
            d.Add("9.5.4.2399", 5349);
            d.Add("9.5.4.2615", 5351);
            d.Add("9.5.4.2753", 5385);
            d.Add("9.5.4.2866", 5386);
            d.Add("9.5.4.5003", 5386);


            d.Add("10.0.0.4207", 625);
            d.Add("10.0.0.4442", 664);
            d.Add("10.0.0.4461", 664);
            d.Add("10.0.1.4854", 689);


            d.Add("11.0.0.353", 7586);
            d.Add("11.0.0.591", 7819);
            d.Add("11.0.0.825", 8073);
            d.Add("11.0.0.837", 8078);
            d.Add("11.0.1.1261", 8378);

            return d;
        }
    }
}
