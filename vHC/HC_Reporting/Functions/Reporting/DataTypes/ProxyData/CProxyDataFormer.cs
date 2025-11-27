// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.DataTypes.ProxyData
{
    internal class CProxyDataFormer
    {
        public CProxyDataFormer() { }

        public void FormData()
        {
        }

        public string CalcProxyTasks(int assignedTasks, int cores, int ram)
        {
            int availableMem = ram ; // TODO double-check OS mem requirements
            int memTasks = (int)Math.Round((decimal)(availableMem / 1), 0, MidpointRounding.ToPositiveInfinity);
            int coreTasks = 0;

            if (cores == 0 && ram == 0)
            {

                return "NA";
            }


            if (CGlobals.VBRMAJORVERSION == 11)
            {
                coreTasks = cores; // TODO need to imrprove this to cover 11a change
                memTasks = this.MemoryTasks(availableMem, 2);
            }
            else if (CGlobals.VBRMAJORVERSION == 12)
            {
                coreTasks = (cores) * 2;
                memTasks = this.MemoryTasks(availableMem, 1);
            }

            return this.SetProvisionStatus(assignedTasks, coreTasks, memTasks);
        }

        private int MemoryTasks(int availableMem, double memoryPerTask)
        {
            return (int)Math.Round((decimal)(availableMem / memoryPerTask), 0, MidpointRounding.ToPositiveInfinity);
        }

        private string SetProvisionStatus(int assignedTasks, int coreTasks, int memTasks)
        {
            CProvisionTypes pt = new();

            if (coreTasks == memTasks)
            {
                if (assignedTasks == memTasks)
                {
                    return pt.WellProvisioned;
                }


                if (assignedTasks > memTasks)
                {

                    return pt.OverProvisioned;
                }

                // if (assignedTasks < memTasks)
                //    return pt.UnderProvisioned;
            }

            if (coreTasks < memTasks)
            {
                if (assignedTasks == coreTasks)
                {
                    return pt.WellProvisioned;
                }

                // if (assignedTasks <= coreTasks)
                //    return pt.UnderProvisioned;

                if (assignedTasks > coreTasks)
                {

                    return pt.OverProvisioned;
                }
            }

            if (coreTasks > memTasks)
            {
                if (assignedTasks == memTasks)
                {
                    return pt.WellProvisioned;
                }

                // if (assignedTasks <= memTasks)
                //    return pt.UnderProvisioned;

                if (assignedTasks > memTasks)
                {

                    return pt.OverProvisioned;
                }
            }

            return "NA";
        }
    }
}
