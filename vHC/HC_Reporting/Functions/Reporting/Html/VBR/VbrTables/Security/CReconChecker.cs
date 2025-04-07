using System;
using Microsoft.Win32.TaskScheduler;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Functions.Reporting.Html.VBR.VbrTables.Security;

public class CReconChecker
{
    //string taskName = "Coveware Recon Healthcheck";
    public void Check()
    {
        string taskName = "Coveware Recon Healthcheck"; // Replace with the name of the task you want to find

        try
        {
            // Create an instance of TaskService
            using (TaskService taskService = new TaskService())
            {
                // Get the task by name
                Task task = taskService.GetTask(taskName);

                if (task != null)
                {
                    // Get the last run time
                    DateTime lastRunTime = task.LastRunTime;

                    // Check if the task has ever run
                    if (lastRunTime == DateTime.MinValue)
                    {
                        CGlobals.Logger.Debug($"The task '{taskName}' has never run.");
                    }
                    else
                    {
                        CGlobals.Logger.Debug($"Task '{taskName}' last ran on: {lastRunTime}");
                        CGlobals.IsReconDetected = true;
                        CGlobals.LastReconRun = lastRunTime;
                    }

                    // Additional task details (optional)
                    CGlobals.Logger.Debug($"Task State: {task.State}");
                    CGlobals.Logger.Debug($"Last Result: {task.LastTaskResult}"); // 0 typically means success
                }
                else
                {
                    CGlobals.Logger.Debug($"Task '{taskName}' not found.");
                }
            }
        }
        catch (Exception ex)
        {
            CGlobals.Logger.Debug($"An error occurred: {ex.Message}");
        }


    }
}
