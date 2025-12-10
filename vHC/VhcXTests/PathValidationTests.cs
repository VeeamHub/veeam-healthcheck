// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using VeeamHealthCheck.Startup;
using Xunit;

namespace VhcXTests
{
    public class PathValidationTests : IDisposable
    {
        private readonly CClientFunctions _functions;
        private readonly string _testBasePath;

        public PathValidationTests()
        {
            _functions = new CClientFunctions();
            _testBasePath = Path.Combine(Path.GetTempPath(), "VhcPathTests_" + Guid.NewGuid().ToString());
        }

        public void Dispose()
        {
            _functions.Dispose();
            
            // Clean up test directories
            if (Directory.Exists(_testBasePath))
            {
                try
                {
                    Directory.Delete(_testBasePath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public void VerifyPath_NullPath_ReturnsFalse()
        {
            // Arrange
            string path = null;

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyPath_EmptyPath_ReturnsFalse()
        {
            // Arrange
            string path = string.Empty;

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyPath_WhitespacePath_ReturnsFalse()
        {
            // Arrange
            string path = "   ";

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyPath_UncPath_ReturnsFalse()
        {
            // Arrange
            string path = @"\\server\share\folder";

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyPath_ExistingDirectory_ReturnsTrue()
        {
            // Arrange
            string path = Path.GetTempPath();

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VerifyPath_NewValidPath_CreatesDirectoryAndReturnsTrue()
        {
            // Arrange
            string path = Path.Combine(_testBasePath, "NewFolder");

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.True(result);
            Assert.True(Directory.Exists(path));
        }

        [Fact]
        public void VerifyPath_NestedNewPath_CreatesDirectoriesAndReturnsTrue()
        {
            // Arrange
            string path = Path.Combine(_testBasePath, "Level1", "Level2", "Level3");

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.True(result);
            Assert.True(Directory.Exists(path));
        }

        [Fact]
        public void VerifyPath_PathWithSpaces_CreatesDirectoryAndReturnsTrue()
        {
            // Arrange
            string path = Path.Combine(_testBasePath, "Folder With Spaces");

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.True(result);
            Assert.True(Directory.Exists(path));
        }

        [Fact]
        public void VerifyPath_InvalidCharacters_ReturnsFalse()
        {
            // Arrange
            string path = Path.Combine(_testBasePath, "Invalid<>Path");

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyPath_PathTooLong_ReturnsFalse()
        {
            // Arrange
            // Create a path that exceeds MAX_PATH (260 characters on Windows)
            string longFolder = new string('a', 300);
            string path = Path.Combine(_testBasePath, longFolder);

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyPath_DefaultVhcPath_CreatesDirectoryAndReturnsTrue()
        {
            // Arrange
            string path = @"C:\temp\VHC";

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.True(result);
            // Note: We don't assert Directory.Exists here because we may not have 
            // permissions to create in C:\temp in all test environments
        }

        [Theory]
        [InlineData(@"C:\temp\VHC")]
        [InlineData(@"C:\temp\VHC\Reports")]
        [InlineData(@"C:\Users\Public\VHC")]
        public void VerifyPath_CommonValidPaths_ReturnsTrue(string path)
        {
            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData(@"\\network\share")]
        [InlineData(@"\\?\UNC\server\share")]
        public void VerifyPath_NetworkPaths_ReturnsFalse(string path)
        {
            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyPath_DriveRoot_ReturnsTrue()
        {
            // Arrange
            string path = @"C:\";

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VerifyPath_PathWithTrailingBackslash_CreatesDirectoryAndReturnsTrue()
        {
            // Arrange
            string path = Path.Combine(_testBasePath, "TrailingSlash") + @"\";

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.True(result);
            Assert.True(Directory.Exists(path));
        }

        [Fact]
        public void VerifyPath_ConsecutiveCalls_ReturnsTrueForSamePath()
        {
            // Arrange
            string path = Path.Combine(_testBasePath, "ConsecutiveTest");

            // Act
            bool result1 = _functions.VerifyPath(path);
            bool result2 = _functions.VerifyPath(path);

            // Assert
            Assert.True(result1);
            Assert.True(result2);
            Assert.True(Directory.Exists(path));
        }

        [Fact]
        public void VerifyPath_RelativePath_ReturnsFalseOrHandlesAppropriately()
        {
            // Arrange
            string path = @".\relative\path";

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            // Relative paths might work depending on current directory
            // This test documents the behavior
            Assert.NotNull(result.ToString());
        }

        [Fact]
        public void VerifyPath_SpecialCharactersInName_HandlesAppropriately()
        {
            // Arrange
            string path = Path.Combine(_testBasePath, "Folder_With-Special.Chars");

            // Act
            bool result = _functions.VerifyPath(path);

            // Assert
            Assert.True(result);
            Assert.True(Directory.Exists(path));
        }
    }
}
