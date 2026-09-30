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
            if (!OperatingSystem.IsWindows())
            {
                return Environment.IsPrivilegedProcess;
            }

            // WindowsIdentity owns a token handle; dispose it (the pre-Avalonia code did).
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
