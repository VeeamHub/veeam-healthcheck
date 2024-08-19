﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VeeamHealthCheck.Functions.Reporting.CsvHandlers;
using VeeamHealthCheck.Functions.Reporting.DataTypes;
using VeeamHealthCheck.Functions.Reporting.Html.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.ProtectedWorkloads
{
    internal class NasSourceInfo
    {
        private CHtmlFormatting _form = new();
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
                foreach (var rec in n)
                {
                    NasWorkloads nas = new();
                    nas.FileShareType = rec.FileShareType;
                    nas.TotalShareSize = CalculateStorageString(rec.TotalShareSize);
                    nas.TotalFilesCount = Convert.ToDouble(rec.TotalFilesCount);
                    nas.TotalFoldersCount = Convert.ToDouble(rec.TotalFoldersCount);

                    if (nas.TotalFilesCount > 0 || nas.TotalFoldersCount > 0 || Convert.ToDouble(rec.TotalShareSize) > 0)
                        p.nasWorkloads.Add(nas);
                }
                var objectShares = c.GetDynamicNasObjectSize();
                foreach (var rec in objectShares)
                {
                    NasWorkloads nas = new();
                    nas.FileShareType = "Object";
                    nas.TotalShareSize = CalculateStorageString(rec.TotalObjectStorageSize);
                    nas.TotalFilesCount = Convert.ToDouble(rec.TotalObjectsCount);

                    if (nas.TotalFilesCount > 0 || Convert.ToDouble(rec.TotalObjectStorageSize) > 0)
                        p.nasWorkloads.Add(nas);
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
            double sizeD = Convert.ToDouble(size);
            // check if size is in TB
            double sizeMB = sizeD / 1024 / 1024;
            double sizeGB = sizeD / 1024 / 1024 / 1024;
            double sizeTB = sizeD / 1024 / 1024 / 1024 / 1024;
            double sizePB = sizeD / 1024 / 1024 / 1024 / 1024 / 1024;
            
            if(sizePB > 1)
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
    }
}
