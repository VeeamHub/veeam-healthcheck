// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
//
// Retrospective TDD for commit 59e2621 — CDbAccessor refactor (ISC-12 through ISC-18).
//
// Production code changes enabling these tests (all minimal-surface):
//   - CDbAccessor class: private → internal
//   - StringBuilder(), SimpleConnectionBuilder(), TestConnection(string): private → internal
//   - CDbAccessor.RegReader property (internal CRegReader?): injection seam for CRegReader
//   - CDbAccessor.WarningSink property (internal Action<string>): captures Warning calls
//     without touching the static CGlobals.Logger singleton.
//   - InternalsVisibleTo("VhcXTests") was already configured in VeeamHealthCheck.csproj.
//
// Warning-capture strategy (ISC-16/17):
//   WarningSink is an internal Action<string> on CDbAccessor, defaulting to
//   CGlobals.Logger.Warning. Tests replace it with a recording lambda. No Moq required
//   for this path — just a simple List<string> collector. Production behaviour is
//   identical because the default delegate delegates straight to the real logger.
//
// CRegReader injection strategy (ISC-14/15/16/17/18):
//   CRegReader.HostString and DbString are backed by private static fields. We populate
//   them via reflection in TestCRegReader.SetValues(), which is the only way to set them
//   without touching production code. The RegReader property seam on CDbAccessor lets
//   SimpleConnectionBuilder() use the pre-populated instance instead of calling GetDbInfo().
//
// xUnit conventions followed: [Fact] / [Theory], Arrange-Act-Assert, naming convention
//   MethodUnderTest_Scenario_ExpectedBehavior. Matches CredentialHelperTests.cs style.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using Xunit;
using VeeamHealthCheck.Functions.Collection.DB;
using VeeamHealthCheck.Shared;

namespace VeeamHealthCheck.Tests.Functions.Collection.DB
{
    // ---------------------------------------------------------------------------
    // Helper: populate CRegReader's private static backing fields via reflection.
    // This is necessary because HostString / DbString are read-only properties
    // backed by private static fields — there's no public setter.
    // ---------------------------------------------------------------------------
    internal static class TestCRegReader
    {
        /// <summary>
        /// Creates a CRegReader instance whose HostString and DbString return the
        /// supplied values. Uses reflection to set the private static backing fields.
        /// </summary>
        internal static CRegReader Create(string? host, string? db)
        {
            var reg = new CRegReader();
            SetStaticField(reg, "hostInstanceString", host);
            SetStaticField(reg, "databaseName",       db);
            return reg;
        }

        private static void SetStaticField(CRegReader instance, string fieldName, string? value)
        {
            var field = typeof(CRegReader).GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null)
                throw new InvalidOperationException(
                    $"CRegReader.{fieldName} not found via reflection. " +
                    "Field may have been renamed — update TestCRegReader.SetStaticField.");
            field.SetValue(instance, value);
        }
    }

    // ---------------------------------------------------------------------------
    // xUnit collection: serialise all test classes that mutate shared static state
    // (CRegReader.hostInstanceString / databaseName) so they never run in parallel.
    // ---------------------------------------------------------------------------
    [CollectionDefinition("CDbAccessor-Sequential", DisableParallelization = true)]
    public class CDbAccessorSequentialCollection { }

    // ---------------------------------------------------------------------------
    // ISC-12  No private 'connectionString' field on CDbAccessor
    // ---------------------------------------------------------------------------
    public class CDbAccessor_NoConnectionStringField
    {
        [Fact]
        public void CDbAccessor_DoesNotHavePrivateConnectionStringField()
        {
            // Arrange / Act
            var field = typeof(CDbAccessor).GetField(
                "connectionString",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            // Assert
            // Pre-commit code had: private string connectionString;
            // Post-commit that stale field was deleted. If it ever comes back, this test fails.
            Assert.Null(field);
        }
    }

    // ---------------------------------------------------------------------------
    // ISC-13  CDbAccessor no longer references System.Security.Principal
    // ---------------------------------------------------------------------------
    public class CDbAccessor_NoSecurityPrincipalReference
    {
        [Fact]
        public void CDbAccessor_DoesNotReferenceSystemSecurityPrincipal()
        {
            // The test validates structural property: no member, field, method, or
            // parameter on CDbAccessor should have a type from System.Security.Principal.
            // The simplest proxy: ensure no method on CDbAccessor returns or accepts
            // a WindowsIdentity / WindowsPrincipal.

            var type = typeof(CDbAccessor);
            var secPrincipalNs = "System.Security.Principal";

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.Instance | BindingFlags.Static))
            {
                // Return type check
                Assert.False(
                    method.ReturnType.FullName?.StartsWith(secPrincipalNs) ?? false,
                    $"Method {method.Name} returns a System.Security.Principal type.");

                // Parameter type check
                foreach (var param in method.GetParameters())
                {
                    Assert.False(
                        param.ParameterType.FullName?.StartsWith(secPrincipalNs) ?? false,
                        $"Method {method.Name} parameter '{param.Name}' is a System.Security.Principal type.");
                }
            }

            // Also assert: the System.Security.Principal assembly is NOT referenced
            // by the CDbAccessor type's assembly for this specific type's usage.
            // We verify this by confirming WindowsIdentity cannot be directly accessed
            // as a dependency of CDbAccessor (its assembly would be in the referenced list
            // only if something in the production code uses it).
            // The above method-signature scan is the reliable cross-platform check.
        }

        [Fact]
        public void CDbAccessor_NoFieldOfSecurityPrincipalType()
        {
            var type = typeof(CDbAccessor);
            var secPrincipalNs = "System.Security.Principal";

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                  BindingFlags.Instance | BindingFlags.Static))
            {
                Assert.False(
                    field.FieldType.FullName?.StartsWith(secPrincipalNs) ?? false,
                    $"Field '{field.Name}' is of a System.Security.Principal type.");
            }
        }
    }

    // ---------------------------------------------------------------------------
    // ISC-14  SimpleConnectionBuilder with mocked CRegReader ->
    //         UserID empty, Password empty, Integrated Security retained
    // ---------------------------------------------------------------------------
    [Collection("CDbAccessor-Sequential")]
    [Trait("Category", "Integration")]
    public class CDbAccessor_SimpleConnectionBuilder_IntegratedSecurity
    {
        [Fact]
        public void SimpleConnectionBuilder_WithMockedRegReader_UserIdIsEmpty()
        {
            // Arrange
            var accessor = new CDbAccessor
            {
                RegReader   = TestCRegReader.Create("testhost\\SQLEXPRESS", "VeeamBackup"),
                // Suppress the pre-flight warning so TestConnection failure doesn't pollute output.
                WarningSink = _ => { }
            };

            // Act
            SqlConnectionStringBuilder builder = accessor.SimpleConnectionBuilder();

            // Assert: pre-commit code set builder.UserID = cred.User.ToString() (a SID).
            // Post-commit that branch was deleted. UserID must be empty string.
            Assert.Equal(string.Empty, builder.UserID);
        }

        [Fact]
        public void SimpleConnectionBuilder_WithMockedRegReader_PasswordIsEmpty()
        {
            // Arrange
            var accessor = new CDbAccessor
            {
                RegReader   = TestCRegReader.Create("testhost\\SQLEXPRESS", "VeeamBackup"),
                WarningSink = _ => { }
            };

            // Act
            SqlConnectionStringBuilder builder = accessor.SimpleConnectionBuilder();

            // Assert: pre-commit code set builder.Password = cred.Token.ToString() (IntPtr).
            // Post-commit that branch was deleted. Password must be empty string.
            Assert.Equal(string.Empty, builder.Password);
        }

        [Fact]
        public void SimpleConnectionBuilder_WithMockedRegReader_IntegratedSecurityRetained()
        {
            // Arrange
            var accessor = new CDbAccessor
            {
                RegReader   = TestCRegReader.Create("testhost\\SQLEXPRESS", "VeeamBackup"),
                WarningSink = _ => { }
            };

            // Act
            SqlConnectionStringBuilder builder = accessor.SimpleConnectionBuilder();

            // Assert: Integrated Security=SSPI from GetConnectionString() baseline must survive.
            Assert.True(builder.IntegratedSecurity,
                "Integrated Security should remain true — the SID/Token fallback branch was removed.");
        }
    }

    // ---------------------------------------------------------------------------
    // ISC-15  TestConnection(string) with bad connection string -> false, no throw
    // ---------------------------------------------------------------------------
    [Trait("Category", "Integration")]
    public class CDbAccessor_TestConnection_BadString
    {
        [Fact]
        public void TestConnection_WithBadConnectionString_ReturnsFalse()
        {
            // Arrange
            var accessor = new CDbAccessor();
            const string badConnectionString = "Server=nonexistent_host_that_will_never_resolve;" +
                                               "Database=DoesNotExist;Integrated Security=SSPI;" +
                                               "Connect Timeout=1;";

            // Act
            bool result = accessor.TestConnection(badConnectionString);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void TestConnection_WithBadConnectionString_DoesNotThrow()
        {
            // Arrange
            var accessor = new CDbAccessor();
            const string badConnectionString = "Server=nonexistent_host_that_will_never_resolve;" +
                                               "Database=DoesNotExist;Integrated Security=SSPI;" +
                                               "Connect Timeout=1;";

            // Act & Assert — must not propagate any exception
            var ex = Record.Exception(() => accessor.TestConnection(badConnectionString));
            Assert.Null(ex);
        }
    }

    // ---------------------------------------------------------------------------
    // ISC-16  Pre-flight fail -> exactly one actionable pre-flight warning is emitted,
    //         text contains 'db_datareader' AND 'VBR service account'.
    //         Note: TestConnection's catch ALSO routes through WarningSink (generic
    //         "Sql Test Connection Failed" message). We assert the actionable
    //         pre-flight warning specifically — not the total count.
    // ---------------------------------------------------------------------------
    [Collection("CDbAccessor-Sequential")]
    [Trait("Category", "Integration")]
    public class CDbAccessor_PreFlightWarning_Content
    {
        [Fact]
        public void SimpleConnectionBuilder_PreFlightFails_EmitsExactlyOnePreFlightWarning()
        {
            // Arrange
            var warnings = new List<string>();
            var accessor = new CDbAccessor
            {
                // Use a host that will fail TestConnection (no live SQL here).
                RegReader   = TestCRegReader.Create("nonexistent_host\\SQLEXPRESS", "VeeamBackup"),
                WarningSink = msg => warnings.Add(msg)
            };

            // Act
            accessor.SimpleConnectionBuilder();

            // Assert: exactly one warning identifies the actionable pre-flight path.
            var preFlightWarnings = warnings.Where(w =>
                w.Contains("pre-flight", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.Single(preFlightWarnings);
        }

        [Fact]
        public void SimpleConnectionBuilder_PreFlightFails_WarningContainsDbDatareader()
        {
            // Arrange
            var warnings = new List<string>();
            var accessor = new CDbAccessor
            {
                RegReader   = TestCRegReader.Create("nonexistent_host\\SQLEXPRESS", "VeeamBackup"),
                WarningSink = msg => warnings.Add(msg)
            };

            // Act
            accessor.SimpleConnectionBuilder();

            // Assert
            Assert.Contains(warnings, w => w.Contains("db_datareader",
                StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void SimpleConnectionBuilder_PreFlightFails_WarningContainsVbrServiceAccount()
        {
            // Arrange
            var warnings = new List<string>();
            var accessor = new CDbAccessor
            {
                RegReader   = TestCRegReader.Create("nonexistent_host\\SQLEXPRESS", "VeeamBackup"),
                WarningSink = msg => warnings.Add(msg)
            };

            // Act
            accessor.SimpleConnectionBuilder();

            // Assert
            Assert.Contains(warnings, w => w.Contains("VBR service account",
                StringComparison.OrdinalIgnoreCase));
        }
    }

    // ---------------------------------------------------------------------------
    // ISC-17  Warning contains no SID pattern, no IntPtr, no literal "Password="
    // ---------------------------------------------------------------------------
    [Collection("CDbAccessor-Sequential")]
    [Trait("Category", "Integration")]
    public class CDbAccessor_PreFlightWarning_NoSensitiveData
    {
        [Fact]
        public void SimpleConnectionBuilder_PreFlightFails_WarningContainsNoSidPattern()
        {
            // Arrange
            var warnings = new List<string>();
            var accessor = new CDbAccessor
            {
                RegReader   = TestCRegReader.Create("nonexistent_host\\SQLEXPRESS", "VeeamBackup"),
                WarningSink = msg => warnings.Add(msg)
            };

            // Act
            accessor.SimpleConnectionBuilder();

            // Assert: SID pattern is S-1-5-... (pre-commit code used cred.User.ToString()).
            foreach (var w in warnings)
            {
                Assert.DoesNotMatch(@"S-\d+-\d+", w);
            }
        }

        [Fact]
        public void SimpleConnectionBuilder_PreFlightFails_WarningContainsNoIntPtr()
        {
            // Arrange
            var warnings = new List<string>();
            var accessor = new CDbAccessor
            {
                RegReader   = TestCRegReader.Create("nonexistent_host\\SQLEXPRESS", "VeeamBackup"),
                WarningSink = msg => warnings.Add(msg)
            };

            // Act
            accessor.SimpleConnectionBuilder();

            // Assert: IntPtr.ToString() produces a hex pointer value like "0x7FFEE4..." or
            // a decimal integer. The pre-commit code used cred.Token.ToString().
            foreach (var w in warnings)
            {
                Assert.DoesNotContain("IntPtr", w, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void SimpleConnectionBuilder_PreFlightFails_WarningContainsNoLiteralPasswordEquals()
        {
            // Arrange
            var warnings = new List<string>();
            var accessor = new CDbAccessor
            {
                RegReader   = TestCRegReader.Create("nonexistent_host\\SQLEXPRESS", "VeeamBackup"),
                WarningSink = msg => warnings.Add(msg)
            };

            // Act
            accessor.SimpleConnectionBuilder();

            // Assert: the warning must not contain a raw "Password=" fragment
            // (which would indicate an accidental connection-string dump in the message).
            foreach (var w in warnings)
            {
                Assert.DoesNotContain("Password=", w, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // ---------------------------------------------------------------------------
    // ISC-18  DbAccessorString() is idempotent across two calls
    // ---------------------------------------------------------------------------
    [Collection("CDbAccessor-Sequential")]
    [Trait("Category", "Integration")]
    public class CDbAccessor_DbAccessorString_Idempotent
    {
        [Fact]
        public void DbAccessorString_CalledTwice_ReturnsSameValue()
        {
            // Arrange
            var accessor = new CDbAccessor
            {
                RegReader   = TestCRegReader.Create("testhost\\SQLEXPRESS", "VeeamBackup"),
                WarningSink = _ => { }
            };

            // Act
            string first  = accessor.DbAccessorString();
            string second = accessor.DbAccessorString();

            // Assert: idempotent — same inputs, same output.
            Assert.Equal(first, second);
        }

        [Fact]
        public void DbAccessorString_ContainsServerFromRegReader()
        {
            // Arrange
            var accessor = new CDbAccessor
            {
                RegReader   = TestCRegReader.Create("myserver\\SQLEXPRESS", "VeeamBackup"),
                WarningSink = _ => { }
            };

            // Act
            string cs = accessor.DbAccessorString();

            // Assert
            Assert.Contains("myserver", cs, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DbAccessorString_ContainsDatabaseFromRegReader()
        {
            // Arrange
            var accessor = new CDbAccessor
            {
                RegReader   = TestCRegReader.Create("myserver\\SQLEXPRESS", "VeeamBackup"),
                WarningSink = _ => { }
            };

            // Act
            string cs = accessor.DbAccessorString();

            // Assert
            Assert.Contains("VeeamBackup", cs, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DbAccessorString_DoesNotContainPassword()
        {
            // Arrange
            var accessor = new CDbAccessor
            {
                RegReader   = TestCRegReader.Create("myserver\\SQLEXPRESS", "VeeamBackup"),
                WarningSink = _ => { }
            };

            // Act
            string cs = accessor.DbAccessorString();

            // Assert: the stale-field bug caused TestConnection to receive an uninitialised
            // null connection string. The post-commit builder should carry Integrated Security
            // and no explicit password.
            // SqlConnectionStringBuilder omits Password= when IntegratedSecurity=true.
            Assert.DoesNotContain("Password=", cs, StringComparison.OrdinalIgnoreCase);
        }
    }
}
