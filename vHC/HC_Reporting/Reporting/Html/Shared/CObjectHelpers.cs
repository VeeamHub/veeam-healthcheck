using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Shared.Common
{
    class CObjectHelpers
    {
        public static bool ParseBool(string value)
        {
            if (value == null) return false;
            else if (value.Length == 0) return false;
            else if (string.IsNullOrEmpty(value)) return false;
            else if (value == "true" || value == "True" || value == "TRUE") return true;
            else if (value == "false" || value == "False" || value == "FALSE") return false;
            else return false;
        }
        public static int ParseInt(string value)
        {
            if (value == null) return 0;
            return int.TryParse(value, out int i) ? i : 0;
        }
    }
}