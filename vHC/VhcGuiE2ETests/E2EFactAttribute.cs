using System;
using Xunit;

// One desktop, one set of windows: the tests must not overlap.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace VhcGuiE2ETests
{
    /// <summary>
    /// A fact that runs only on Windows with VHC_E2E_EXE set. Anywhere else it is skipped with the
    /// reason, so <c>dotnet test vHC/HC.sln</c> in build-and-test and pr-release-prep stays green.
    /// A VHC_E2E_EXE that is set but wrong is NOT a skip: it fails loudly in the session.
    /// </summary>
    public sealed class E2EFactAttribute : FactAttribute
    {
        public const string ExeVariable = "VHC_E2E_EXE";
        public const string LiveVariable = "VHC_E2E_LIVE";

        public E2EFactAttribute(bool requiresLiveVbr = false)
        {
            if (!OperatingSystem.IsWindows())
            {
                this.Skip = "GUI E2E needs Windows (FlaUI drives the Win32/UIA tree).";
            }
            else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ExeVariable)))
            {
                this.Skip = $"Set {ExeVariable} to the published VeeamHealthCheck.exe to run the GUI E2E tests.";
            }
            else if (requiresLiveVbr && Environment.GetEnvironmentVariable(LiveVariable) != "1")
            {
                this.Skip = $"A full Run needs a local VBR server; set {LiveVariable}=1 on the lab runner to enable it.";
            }
        }
    }
}
