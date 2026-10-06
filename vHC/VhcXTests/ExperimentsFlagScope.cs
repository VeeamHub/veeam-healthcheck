// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using VeeamHealthCheck.Shared;

namespace VhcXTests
{
    // Sets VHC_EXPERIMENTS for the lifetime of a `using` block and restores whatever
    // was there before, even if the test throws. Passing null removes the variable.
    // Only use from classes marked [Collection("GlobalState")]: environment variables
    // are process-wide, so a parallel test would see the change.
    internal sealed class ExperimentsFlagScope : IDisposable
    {
        private readonly string? original;

        public ExperimentsFlagScope(string? value)
        {
            this.original = Environment.GetEnvironmentVariable(CFeatureFlags.ExperimentsEnvVar);
            Environment.SetEnvironmentVariable(CFeatureFlags.ExperimentsEnvVar, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(CFeatureFlags.ExperimentsEnvVar, this.original);
        }
    }
}
