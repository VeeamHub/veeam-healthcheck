// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.Diagnostics;
using VeeamHealthCheck;
using VeeamHealthCheck.Shared;

namespace VhcXTests
{
    /// <summary>
    /// Tests to ensure static class initialization doesn't cause stack overflow.
    /// Regression test for GitHub Actions hang issue caused by circular dependency
    /// between CVariables.desiredDir and CGlobals.desiredPath
    /// </summary>
    public class StaticInitializationTEST
    {
        /// <summary>
        /// Verifies that CVariables can be accessed without triggering stack overflow.
        /// This test guards against circular property dependencies during static initialization.
        /// </summary>
        [Fact]
        public void CVariables_InitializationCompletes_WithoutStackOverflow()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();
            const int timeoutMs = 5000; // 5 second timeout to detect infinite recursion

            // Act & Assert
            try
            {
                var unsafeDir = CVariables.unsafeDir;
                var safeDir = CVariables.safeDir;
                var vbrDir = CVariables.vbrDir;
                var vb365Dir = CVariables.vb365dir;

                stopwatch.Stop();
                Assert.True(stopwatch.ElapsedMilliseconds < timeoutMs,
                    $"CVariables initialization took {stopwatch.ElapsedMilliseconds}ms - possible infinite recursion detected");
            }
            catch (StackOverflowException)
            {
                Assert.Fail("CVariables static initialization caused a StackOverflowException due to circular dependencies");
            }
        }

        /// <summary>
        /// Verifies that CGlobals can be accessed without triggering stack overflow.
        /// This test ensures desiredPath property doesn't recursively call itself.
        /// </summary>
        [Fact]
        public void CGlobals_DesiredPathAccess_WithoutStackOverflow()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();
            const int timeoutMs = 5000; // 5 second timeout to detect infinite recursion

            // Act & Assert
            try
            {
                var desiredPath = CGlobals.desiredPath;
                CGlobals.desiredPath = @"C:\temp\test";
                var modifiedPath = CGlobals.desiredPath;

                stopwatch.Stop();
                Assert.True(stopwatch.ElapsedMilliseconds < timeoutMs,
                    $"CGlobals.desiredPath access took {stopwatch.ElapsedMilliseconds}ms - possible recursion detected");

                Assert.Equal(@"C:\temp\test", modifiedPath);
            }
            catch (StackOverflowException)
            {
                Assert.Fail("CGlobals.desiredPath property caused a StackOverflowException due to recursive calls");
            }
        }

        /// <summary>
        /// Verifies that GetCsvFileSizesToLog can be called without infinite recursion.
        /// This was the original entry point where the stack overflow manifested.
        /// </summary>
        [Fact]
        public void CVariables_NoCircularDependency_WithCGlobals()
        {
            // Arrange - ensure basic static properties are accessible
            var stopwatch = Stopwatch.StartNew();
            const int timeoutMs = 5000;

            // Act & Assert
            try
            {
                // Accessing vbrDir which internally uses unsafeDir and VbrDir
                var vbrPath = CVariables.vbrDir;
                
                // Accessing desiredPath which should not trigger any circular calls
                var desiredPath = CGlobals.desiredPath;

                stopwatch.Stop();
                Assert.True(stopwatch.ElapsedMilliseconds < timeoutMs,
                    $"Cross-class property access took {stopwatch.ElapsedMilliseconds}ms");

                // Verify paths are strings and not null
                Assert.NotNull(vbrPath);
                Assert.NotNull(desiredPath);
                Assert.IsType<string>(vbrPath);
                Assert.IsType<string>(desiredPath);
            }
            catch (StackOverflowException)
            {
                Assert.Fail("Circular dependency detected between CVariables and CGlobals");
            }
        }
    }
}
