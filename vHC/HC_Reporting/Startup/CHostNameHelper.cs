// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;

namespace VeeamHealthCheck.Startup
{
    /// <summary>
    /// Helper class for hostname detection and comparison.
    /// Addresses GitHub Issue #82: Tool treats local machine as remote when using /host= parameter.
    /// </summary>
    internal static class CHostNameHelper
    {
        /// <summary>
        /// Determines if the provided hostname refers to the local machine.
        /// Handles: localhost, 127.0.0.1, machine name, DNS hostname, and FQDN variants.
        /// </summary>
        /// <param name="hostname">The hostname to check</param>
        /// <returns>True if the hostname refers to the local machine, false otherwise</returns>
        public static bool IsLocalHost(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                return false;

            // Check special local values
            if (string.Equals(hostname, "localhost", StringComparison.OrdinalIgnoreCase))
                return true;

            if (hostname == "127.0.0.1")
                return true;

            // Get local machine identifiers
            string machineName = Environment.MachineName;
            string dnsHostName;

            try
            {
                dnsHostName = System.Net.Dns.GetHostName();
            }
            catch
            {
                // If DNS lookup fails, fall back to machine name only
                dnsHostName = machineName;
            }

            // Case-insensitive comparison against machine name
            if (string.Equals(hostname, machineName, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(hostname, dnsHostName, StringComparison.OrdinalIgnoreCase))
                return true;

            // Handle FQDN - hostname.domain.com should match if hostname matches local machine
            // The dot after the machine name ensures we don't match partial names like "SERVER1-backup"
            if (hostname.StartsWith(machineName + ".", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.Equals(dnsHostName, machineName, StringComparison.OrdinalIgnoreCase) &&
                hostname.StartsWith(dnsHostName + ".", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
    }
}
