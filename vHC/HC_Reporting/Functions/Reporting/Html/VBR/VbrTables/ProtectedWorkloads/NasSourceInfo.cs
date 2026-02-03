using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers.VB365;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;
using VeeamHealthCheck.Shared;
using VeeamHealthCheck.Shared.Logging;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.ProtectedWorkloads
{
    internal class NasSourceInfo
    {
        private readonly CHtmlFormatting form = new();

        public NasSourceInfo()
        {
        }

        public CProtectedWorkloads NasTable()
        {
            CProtectedWorkloads p = new();

            try
            {
                CCsvParser c = new();
                var n = c.GetDynamicNasShareSize();
                
                // Handle case where NAS data is not available (e.g., remote execution)
                if (n != null)
                {
                    foreach (var rec in n)
                    {
                        if(rec.BackupMode != string.Empty)
                        {
                            NasWorkloads nas = new();
                            nas.FileShareType = rec.FileShareType;
                            nas.TotalShareSize = this.CalculateStorageString(rec.TotalShareSize);
                            nas.TotalFilesCount = Convert.ToDouble(rec.TotalFilesCount);
                            nas.TotalFoldersCount = Convert.ToDouble(rec.TotalFoldersCount);
                            p.nasWorkloads.Add(nas);
                        }

                        // if (nas.TotalFilesCount > 0 || nas.TotalFoldersCount > 0 || Convert.ToDouble(rec.TotalShareSize) > 0)
                        //    p.nasWorkloads.Add(nas);
                    }
                }
                else
                {
                    CGlobals.Logger.Info("NAS share size data not available - this is expected for remote execution", false);
                }

                var objectShares = c.GetDynamicNasObjectSize();
                if (objectShares != null)
                {
                    foreach (var rec in objectShares)
                    {
                        NasWorkloads nas = new();
                        nas.FileShareType = "Object";
                        nas.TotalShareSize = this.CalculateStorageString(rec.TotalObjectStorageSize);
                        nas.TotalFilesCount = Convert.ToDouble(rec.TotalObjectsCount);

                        if (nas.TotalFilesCount > 0 || Convert.ToDouble(rec.TotalObjectStorageSize) > 0)
                        {
                            p.nasWorkloads.Add(nas);
                        }
                    }
                }
                else
                {
                    CGlobals.Logger.Info("NAS object size data not available - this is expected for remote execution", false);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return p;
        }

        private string CalculateStorageString(string size)
        {
            try
            {
                double sizeD = Convert.ToDouble(size);

                // check if size is in TB
                double sizeMB = sizeD / 1024 / 1024;
                double sizeGB = sizeD / 1024 / 1024 / 1024;
                double sizeTB = sizeD / 1024 / 1024 / 1024 / 1024;
                double sizePB = sizeD / 1024 / 1024 / 1024 / 1024 / 1024;

                if (sizePB > 1)
                {
                    return $"{sizePB:0.00} PB";
                }
                else if (sizeTB > 1)
                {
                    return $"{sizeTB:0.00} TB";
                }
                else if (sizeGB > 1)
                {
                    return $"{sizeGB:0.00} GB";
                }
                else if (sizeMB > 1)
                {
                    return $"{sizeMB:0.00} MB";
                }
                else
                {
                    return $"{sizeD:0.00} KB";
                }
            }
            catch(Exception ex)
            {
                CGlobals.Logger.Warning("Failed to parse NAS source size");
                CGlobals.Logger.Warning(ex.Message);
                return "Undetermined";
            }
        }
    }
}
