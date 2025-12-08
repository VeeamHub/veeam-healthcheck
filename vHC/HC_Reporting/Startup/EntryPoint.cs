// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Startup
{
    public class EntryPoint
    {
        private static readonly CClientFunctions functions = new();

        [STAThread]
        public static int Main(string[] args)
        {
            // Fix for single-file extraction: ensure .NET can extract embedded files
            // even if TEMP/TMP environment variables are missing or invalid
            EnsureBundleExtractionPath();
            
            CGlobals.Logger.Debug("Starting the application");
            CGlobals.Logger.Debug("The arguments are: " + string.Join(" ", args));

            try
            {
                CArgsParser ap = new(args);
                var res =  ap.InitializeProgram();
                CGlobals.Logger.Info("The result is: " + res, true);
                return 0;
            }
            catch (Exception ex) {
                CGlobals.Logger.Error("Exception occurred: " + ex.Message);
                CGlobals.Logger.Error("Stack trace: " + ex.StackTrace);
                if (ex.InnerException != null)
                {
                    CGlobals.Logger.Error("Inner exception: " + ex.InnerException.Message);
                    CGlobals.Logger.Error("Inner stack trace: " + ex.InnerException.StackTrace);
                }

                CGlobals.Logger.Error("The result is: " + 1, true);
                return 1;
            }
        }
        
        private static void EnsureBundleExtractionPath()
        {
            try
            {
                // Check if extraction path is already set
                var existingPath = Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR");
                if (!string.IsNullOrEmpty(existingPath) && Directory.Exists(existingPath))
                {
                    return; // Already configured
                }
                
                // Try to use existing TEMP directory
                var tempPath = Path.GetTempPath();
                if (!string.IsNullOrEmpty(tempPath) && Directory.Exists(tempPath))
                {
                    var extractPath = Path.Combine(tempPath, "VeeamHealthCheck-Extract");
                    Directory.CreateDirectory(extractPath);
                    Environment.SetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR", extractPath);
                    return;
                }
                
                // Fallback: use a path relative to the executable
                var exeDir = AppContext.BaseDirectory;
                var fallbackPath = Path.Combine(exeDir, ".extract");
                Directory.CreateDirectory(fallbackPath);
                Environment.SetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR", fallbackPath);
            }
            catch (Exception ex)
            {
                // If all else fails, log but continue - .NET might still find a path
                Console.WriteLine($"Warning: Could not set bundle extraction path: {ex.Message}");
            }
        }
    }
}
