// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Security.Principal;

namespace VeeamHealthCheck
{
    public class CAdminCheck
    {
        public bool IsAdmin()
        {
            return OperatingSystem.IsWindows()
                ? new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)
                : Environment.IsPrivilegedProcess;
        }
    }
}
