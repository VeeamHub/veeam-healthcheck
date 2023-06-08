// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System.Security.Principal;

namespace VeeamHealthCheck
{
    public class CAdminCheck
    {
        public bool IsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                bool IsAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                return IsAdmin;
            }
        }
    }
}
