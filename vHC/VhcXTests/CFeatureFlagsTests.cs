// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests
{
    [Collection("GlobalState")]
    public class CFeatureFlagsTests
    {
        [Theory]
        [InlineData("1")]
        [InlineData("true")]
        [InlineData("TRUE")]
        [InlineData("True")]
        [InlineData(" 1 ")]
        [InlineData("  true  ")]
        public void ExperimentsEnabled_AllowlistedValue_ReturnsTrue(string value)
        {
            using var scope = new ExperimentsFlagScope(value);

            Assert.True(CFeatureFlags.ExperimentsEnabled);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("0")]
        [InlineData("false")]
        [InlineData("FALSE")]
        [InlineData("yes")]
        [InlineData("on")]
        [InlineData("enabled")]
        [InlineData("ture")]
        [InlineData("2")]
        [InlineData("11")]
        [InlineData("true1")]
        public void ExperimentsEnabled_AnyOtherValue_ReturnsFalse(string? value)
        {
            using var scope = new ExperimentsFlagScope(value);

            Assert.False(CFeatureFlags.ExperimentsEnabled);
        }

        [Fact]
        public void ExperimentsEnabled_EnvironmentChangesBetweenCalls_ReflectsLatestValue()
        {
            using var scope = new ExperimentsFlagScope("1");
            Assert.True(CFeatureFlags.ExperimentsEnabled);

            Environment.SetEnvironmentVariable(CFeatureFlags.ExperimentsEnvVar, "0");

            Assert.False(CFeatureFlags.ExperimentsEnabled);
        }

        [Fact]
        public void ExperimentsFlagScope_Dispose_RestoresOriginalValue()
        {
            using (new ExperimentsFlagScope("true"))
            {
                using (new ExperimentsFlagScope(null))
                {
                    Assert.False(CFeatureFlags.ExperimentsEnabled);
                }

                Assert.True(CFeatureFlags.ExperimentsEnabled);
            }
        }
    }
}
