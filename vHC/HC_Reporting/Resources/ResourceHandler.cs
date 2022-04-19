// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Resources;
using System.Globalization;
using System.Reflection;
using System.IO;

namespace VeeamHealthCheck
{
    class ResourceHandler
    {
        private static ResourceManager m4 = new("VeeamHealthCheck.Resources.vhcres", typeof(ResourceHandler).Assembly);

        public static string test2 = m4.GetString("HtmlIntroLine5");
        public ResourceHandler()
        {

        }
        public static string GetResourceText(string resource)
        {
            //Test();
            CultureInfo ci = CultureInfo.CurrentCulture;
            ResourceManager rm2 = new(typeof(Resources.VhcResources));
            //ResourceManager m3 = new("VeeamHealthCheck.Resources.BaseResources", typeof(ResourceHandler).Assembly);
            Test();
            
            return rm2.GetString(resource, ci);

        }
        private static void Test()
        {
            ResourceManager m3 = new("VeeamHealthCheck.Resources.BaseResources", typeof(ResourceHandler).Assembly);
            string test = m3.GetString("HtmlIntroLine3");
            
            ResourceManager m4 = new("VeeamHealthCheck.Resources.vhcres", typeof (ResourceHandler).Assembly);

            string test2 = m4.GetString("HtmlIntroLine5");
        }
    }
}
