// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;

namespace VeeamHealthCheck
{
    public class CAdminCheck
    {
        public bool IsAdmin()
        {
            return Environment.IsPrivilegedProcess;
        }
    }
}
