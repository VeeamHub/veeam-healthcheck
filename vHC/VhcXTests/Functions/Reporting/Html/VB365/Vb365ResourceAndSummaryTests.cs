// Copyright (C) 2025 VeeamHub
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using VeeamHealthCheck.Functions.Reporting.Html.VB365;
using VeeamHealthCheck.Resources.Localization.VB365;
using Xunit;

namespace VhcXTests.Functions.Reporting.Html.VB365
{
    [Trait("Category", "Unit")]
    public class Vb365ResourceHandlerTests
    {
        private static readonly FieldInfo[] StringFields = typeof(Vb365ResourceHandler)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .ToArray();

        [Fact]
        public void PublicStaticStrings_AreDiscovered()
        {
            Assert.True(StringFields.Length > 100, $"expected many resource strings, found {StringFields.Length}");
        }

        [Fact]
        public void PublicStaticStrings_AreAllNonNull()
        {
            string[] missing = StringFields.Where(f => f.GetValue(null) is null).Select(f => f.Name).ToArray();

            Assert.True(missing.Length == 0, "Resource keys missing from vb365_vhcres: " + string.Join(", ", missing));
        }

        [Fact]
        public void PublicStaticStrings_AreNonNullAndNonEmpty()
        {
            string[] bad = StringFields
                .Where(f => string.IsNullOrWhiteSpace((string?)f.GetValue(null)))
                .Select(f => f.Name)
                .ToArray();

            Assert.True(bad.Length == 0, "Null or empty resource strings: " + string.Join(", ", bad));
        }

        [Fact]
        public void NavigationTitles_AreNonEmptyForEverySlot()
        {
            for (int i = 0; i <= 14; i++)
            {
                var title = typeof(Vb365ResourceHandler).GetField("v365NavTitle" + i)?.GetValue(null) as string;
                var value = typeof(Vb365ResourceHandler).GetField("v365NavValue" + i)?.GetValue(null) as string;

                Assert.False(string.IsNullOrWhiteSpace(title), $"v365NavTitle{i}");
                Assert.False(string.IsNullOrWhiteSpace(value), $"v365NavValue{i}");
            }
        }
    }

    [Trait("Category", "Unit")]
    public class CM365SummariesTests
    {
        private static readonly Func<CM365Summaries, string>[] Summaries =
        {
            s => s.GlobalSummary(), s => s.ProxySummary(), s => s.RepoSummary(), s => s.RbacSummary(),
            s => s.SecSummary(), s => s.ControllerSummary(), s => s.ControllerDrivesSummary(),
            s => s.JobSessSummary(), s => s.JobStatSummary(), s => s.JobsSummary(), s => s.ObjRepoSummary(),
            s => s.OrgSummary(), s => s.PermissionSummary(), s => s.ProtStatSummary(),
        };

        public static TheoryData<int> SummaryIndexes()
        {
            var data = new TheoryData<int>();
            for (int i = 0; i < Summaries.Length; i++)
            {
                data.Add(i);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(SummaryIndexes))]
        public void Summary_AnySection_StartsWithCollapsibleButtonAndContentDiv(int index)
        {
            string html = Summaries[index](new CM365Summaries());

            Assert.StartsWith("<button", html);
            Assert.Contains("Show Summary", html);
            Assert.Contains("<div class=\"content\">", html);
        }

        // RbacSummary (3) and PermissionSummary (12) are empty placeholders for tables the report does not render.
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        [InlineData(11)]
        [InlineData(13)]
        public void Summary_RenderedSections_HaveNoEmptyTextBlocks(int index)
        {
            string html = Summaries[index](new CM365Summaries());

            Assert.DoesNotContain("<div class=\"hdr\"></div>", html);
            Assert.DoesNotContain("<div class=\"i2\"></div>", html);
            Assert.DoesNotContain("<div class=\"i3\"></div>", html);
        }

        [Theory]
        [MemberData(nameof(SummaryIndexes))]
        public void Summary_AnySection_ContainsAtLeastOneNonEmptyParagraph(int index)
        {
            string html = Summaries[index](new CM365Summaries());

            Assert.Matches("<div class=\"(hdr|i2|i3)\">[^<]+</div>", html);
        }

        [Fact]
        public void GlobalSummary_ListsSummaryAndNotesBlocks()
        {
            string html = new CM365Summaries().GlobalSummary();

            Assert.Contains(Vb365ResourceHandler.GlobalSummaryHeader, html);
            Assert.Contains(Vb365ResourceHandler.GlobalSummary1, html);
            Assert.Contains(Vb365ResourceHandler.GlobalNotesHeader, html);
            Assert.Contains(Vb365ResourceHandler.GlobalNotes1, html);
        }
    }
}
