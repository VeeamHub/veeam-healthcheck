// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
using System;
using System.IO;
using VeeamHealthCheck.Scrubber;
using VeeamHealthCheck.Shared;
using Xunit;

namespace VhcXTests.Common.Scrubber
{
    /// <summary>
    /// ScrubItem must scrub every real name, including one that happens to look like a token
    /// ("Job_Nightly"), and still return its own tokens unchanged so a value scrubbed twice in
    /// one run keeps its token. Only synthetic names are used here.
    /// </summary>
    [Collection("GlobalState")]
    [Trait("Category", "Scrubbing")]
    public class CScrubHandlerTokenTests : IDisposable
    {
        private readonly string originalDesiredPath;
        private readonly string tempDir;
        private readonly CScrubHandler scrubber;

        public CScrubHandlerTokenTests()
        {
            this.originalDesiredPath = CGlobals.desiredPath;

            // ScrubItem persists its key file under CVariables.unsafeDir; keep it out of the repo.
            this.tempDir = Path.Combine(Path.GetTempPath(), "vhc-scrub-token-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(this.tempDir, "Original"));
            CGlobals.desiredPath = this.tempDir;

            this.scrubber = new CScrubHandler();
        }

        public void Dispose()
        {
            CGlobals.desiredPath = this.originalDesiredPath;
            try { Directory.Delete(this.tempDir, recursive: true); } catch { /* best effort */ }
        }

        [Theory]
        [InlineData("Job_Nightly", ScrubItemType.Job)]
        [InlineData("Job_Synthetic_Alpha", ScrubItemType.Job)]
        [InlineData("VM_synthetic-alpha-01", ScrubItemType.VM)]
        [InlineData("Server_synthetic-proxy", ScrubItemType.Server)]
        [InlineData("Repository_Synthetic", ScrubItemType.Repository)]
        public void ScrubItem_RealNameStartingWithTypePrefix_IsScrubbed(string realName, ScrubItemType type)
        {
            string token = this.scrubber.ScrubItem(realName, type);

            Assert.NotEqual(realName, token);
            Assert.StartsWith(type + "_", token);
            Assert.DoesNotContain(realName, this.scrubber.FinalizeScrubbedText("<td>" + realName + "</td>"));
        }

        [Fact]
        public void ScrubItem_TokenItIssued_IsReturnedUnchangedAndRegistersNothing()
        {
            string token = this.scrubber.ScrubItem("Synthetic-Job-Alpha", ScrubItemType.Job);

            Assert.Equal(token, this.scrubber.ScrubItem(token, ScrubItemType.Job));

            // Re-scrubbing the token must not have consumed a counter value.
            Assert.Equal("Job_1", this.scrubber.ScrubItem("Synthetic-Job-Beta", ScrubItemType.Job));
        }

        [Fact]
        public void ScrubItem_RealNameShapedLikeAFutureToken_NeverSharesATokenWithAnotherName()
        {
            // "Job_1" is a real job name here. The token later issued to another job must not be
            // "Job_1", or the final pass (original "Job_1" -> its token) would rewrite that token.
            string realToken = this.scrubber.ScrubItem("Job_1", ScrubItemType.Job);
            string alphaToken = this.scrubber.ScrubItem("Synthetic-Job-Alpha", ScrubItemType.Job);

            Assert.NotEqual("Job_1", realToken);
            Assert.NotEqual("Job_1", alphaToken);
            Assert.NotEqual(realToken, alphaToken);

            string html = this.scrubber.FinalizeScrubbedText("<td>" + realToken + "</td><td>" + alphaToken + "</td>");
            Assert.Equal("<td>" + realToken + "</td><td>" + alphaToken + "</td>", html);
        }
    }
}
