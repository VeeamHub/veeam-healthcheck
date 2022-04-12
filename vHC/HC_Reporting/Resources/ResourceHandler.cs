// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Resources;
using System.Globalization;
using System.Reflection;

namespace VeeamHealthCheck
{
    class ResourceHandler
    {
        public ResourceHandler()
        {

        }
        public static string GetResourceText(string resource)
        {
            //Test();
            CultureInfo ci = CultureInfo.CurrentCulture;
            ResourceManager rm2 = new(typeof(Resources.VhcResources));
            //ResourceManager m3 = new("VeeamHealthCheck.Resources.BaseResources", typeof(ResourceHandler).Assembly);
            return rm2.GetString(resource, ci);

        }
        private static void Test()
        {
            ResourceManager m3 = new("VeeamHealthCheck.Resources.BaseResources", typeof(ResourceHandler).Assembly);
            string test = m3.GetString("GuiRunButton");
        }
    }
}
