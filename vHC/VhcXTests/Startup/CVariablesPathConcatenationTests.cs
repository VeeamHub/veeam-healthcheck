// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using VeeamHealthCheck;
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests.Startup
{
    /// <summary>
    /// Guards CVariables.vb365dir/vbrDir/GetVbrBaseDir against reverting to raw '+'
    /// string concatenation of unsafeDir (already Path.Combine-built) with the
    /// "\VB365"/"\VBR" literals. That never breaks real Windows production
    /// (backslash literals work fine there), but breaks the moment these getters
    /// are exercised by tests on non-Windows CI, which is exactly what this
    /// initiative needs to be trustworthy.
    /// </summary>
    [Trait("Category", "Unit")]
    [Collection("GlobalState")]
    public class CVariablesPathConcatenationTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _origDesiredPath;
        private readonly bool _origImport;
        private readonly string _origImportPath;
        private readonly string _origResolvedImportPath;

        public CVariablesPathConcatenationTests()
        {
            _origDesiredPath = CGlobals.desiredPath;
            _origImport = CGlobals.IMPORT;
            _origImportPath = CGlobals.IMPORT_PATH;
            _origResolvedImportPath = CVariables.ResolvedImportPath;

            _tempDir = Path.Combine(Path.GetTempPath(), "VhcPathConcatTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);

            CGlobals.desiredPath = _tempDir;

            // Explicitly fall through to the non-import branch: vb365dir/vbrDir check
            // CGlobals.IMPORT first, and the raw-concatenation bug only lives in the
            // non-import branch.
            CGlobals.IMPORT = false;
            CGlobals.IMPORT_PATH = null;
            CVariables.ResolvedImportPath = null;
        }

        public void Dispose()
        {
            CGlobals.desiredPath = _origDesiredPath;
            CGlobals.IMPORT = _origImport;
            CGlobals.IMPORT_PATH = _origImportPath;
            CVariables.ResolvedImportPath = _origResolvedImportPath;

            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
            }
        }

        [Fact]
        public void Vb365dir_NotImporting_JoinsUnsafeDirAndVb365DirViaPathCombine()
        {
            string expected = Path.Combine(_tempDir, "Original", "VB365");

            Assert.Equal(expected, CVariables.vb365dir);
        }

        [Fact]
        public void VbrDir_NotImporting_StartsWithUnsafeDirAndVbrDirJoinedViaPathCombine()
        {
            string expectedBase = Path.Combine(_tempDir, "Original", "VBR");

            // Not an exact match: GetVbrDirWithTimestamp() appends server name and run
            // timestamp segments after the base.
            Assert.StartsWith(expectedBase, CVariables.vbrDir);
        }

        [Fact]
        public void GetVbrBaseDir_NotImporting_EqualsUnsafeDirAndVbrDirJoinedViaPathCombine()
        {
            string expected = Path.Combine(_tempDir, "Original", "VBR");

            Assert.Equal(expected, CVariables.GetVbrBaseDir());
        }
    }
}
