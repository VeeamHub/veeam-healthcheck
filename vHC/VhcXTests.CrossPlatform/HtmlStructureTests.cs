// Copyright (c) 2021, Adam Congdon <adam.congdon2@gmail.com>
// MIT License
// Cross-platform tests for HTML structure validation (Phase 0)

using System;
using System.Text.RegularExpressions;
using Xunit;

namespace VhcXTests.CrossPlatform;

/// <summary>
/// Tests that validate HTML structural correctness patterns.
/// These serve as regression guardrails for the HTML builder migration.
/// </summary>
public class HtmlStructureTests
{
    #region Helper Methods

    /// <summary>
    /// Counts occurrences of a pattern in the HTML string.
    /// </summary>
    private static int CountPattern(string html, string pattern)
    {
        return Regex.Matches(html, pattern, RegexOptions.IgnoreCase).Count;
    }

    #endregion

    #region Test 1: Table tags always match

    [Fact]
    public void Table_OpenAndCloseTags_AlwaysMatch()
    {
        // Well-formed table
        string html = "<table><thead><tr><th>Header</th></tr></thead><tbody><tr><td>Data</td></tr></tbody></table>";

        int opens = CountPattern(html, @"<table[\s>]");
        int closes = CountPattern(html, @"</table>");

        Assert.Equal(opens, closes);
    }

    [Fact]
    public void Table_MismatchDetected_WhenExtraCloseTag()
    {
        // Malformed: double </table> (the kind of bug found in the gap analysis)
        string malformed = "<table><tr><td>Data</td></tr></table><br></table>";

        int opens = CountPattern(malformed, @"<table[\s>]");
        int closes = CountPattern(malformed, @"</table>");

        // This should NOT be equal -- proving we can detect the bug
        Assert.NotEqual(opens, closes);
    }

    #endregion

    #region Test 2: Thead contains only th elements (no td, no p)

    [Fact]
    public void Thead_ContainsOnlyThElements_WellFormed()
    {
        string html = "<thead><tr><th title=\"tip\">Name</th><th>Value</th></tr></thead>";

        // Should have no <td> inside thead
        string theadContent = ExtractTagContent(html, "thead");
        Assert.DoesNotContain("<td", theadContent);
        Assert.DoesNotContain("<p>", theadContent);
        Assert.DoesNotContain("<p ", theadContent);
    }

    [Fact]
    public void Thead_DetectsMalformedParagraphInside()
    {
        // Known bug pattern from gap analysis: <p> inside <thead>
        string malformed = "<thead><tr><p>No modified registry keys found</p></tr></thead>";

        string theadContent = ExtractTagContent(malformed, "thead");
        Assert.Contains("<p>", theadContent);
    }

    #endregion

    #region Test 3: No double <tr><tr> patterns

    [Fact]
    public void Table_NoDoubleTrOpens_WellFormed()
    {
        string html = "<table><tbody><tr><td>A</td></tr><tr><td>B</td></tr></tbody></table>";

        bool hasDoubleTr = Regex.IsMatch(html, @"<tr>\s*<tr>", RegexOptions.IgnoreCase);
        Assert.False(hasDoubleTr, "Well-formed HTML should not have <tr><tr> pattern");
    }

    [Fact]
    public void Table_DetectsDoubleTrPattern()
    {
        // Known bug pattern from gap analysis: 25 occurrences of <tr><tr>
        string malformed = "<table><tbody><tr><tr><td>A</td></tr></tr></tbody></table>";

        bool hasDoubleTr = Regex.IsMatch(malformed, @"<tr>\s*<tr>", RegexOptions.IgnoreCase);
        Assert.True(hasDoubleTr, "Should detect <tr><tr> pattern");
    }

    #endregion

    #region Test 4: tbody exists when thead exists

    [Fact]
    public void Table_TbodyExistsWhenTheadExists_WellFormed()
    {
        string html = "<table><thead><tr><th>H</th></tr></thead><tbody><tr><td>D</td></tr></tbody></table>";

        bool hasThead = html.Contains("<thead>", StringComparison.OrdinalIgnoreCase);
        bool hasTbody = html.Contains("<tbody>", StringComparison.OrdinalIgnoreCase);

        if (hasThead)
        {
            Assert.True(hasTbody, "When <thead> is present, <tbody> must also be present");
        }
    }

    [Fact]
    public void Table_MissingTbodyDetected_WhenTheadPresent()
    {
        // Table with thead but no tbody
        string malformed = "<table><thead><tr><th>H</th></tr></thead><tr><td>D</td></tr></table>";

        bool hasThead = malformed.Contains("<thead>", StringComparison.OrdinalIgnoreCase);
        bool hasTbody = malformed.Contains("<tbody>", StringComparison.OrdinalIgnoreCase);

        Assert.True(hasThead);
        Assert.False(hasTbody, "Should detect missing <tbody> when <thead> is present");
    }

    #endregion

    #region Test 5: th not directly inside table (must be in thead/tr)

    [Fact]
    public void Table_ThNotDirectlyInsideTable_WellFormed()
    {
        string html = "<table><thead><tr><th>H</th></tr></thead><tbody><tr><td>D</td></tr></tbody></table>";

        // After <table>, the next tag should be <thead> or <tbody>, not <th> directly
        // We check that <table> is NOT immediately followed by <th> (without thead/tr wrapper)
        bool thDirectlyAfterTable = Regex.IsMatch(html, @"<table[^>]*>\s*<th[\s>]", RegexOptions.IgnoreCase);
        Assert.False(thDirectlyAfterTable, "<th> should not appear directly inside <table> without <thead><tr> wrapper");
    }

    [Fact]
    public void Table_DetectsThDirectlyInsideTable()
    {
        // Known bug pattern: <table><th without <thead><tr>
        string malformed = "<table><th title=\"\">Header</th></table>";

        bool thDirectlyAfterTable = Regex.IsMatch(malformed, @"<table[^>]*>\s*<th[\s>]", RegexOptions.IgnoreCase);
        Assert.True(thDirectlyAfterTable, "Should detect <th> directly inside <table>");
    }

    #endregion

    #region Test 6: Section cards have matching open/close divs

    [Fact]
    public void SectionCard_DivsAreBalanced()
    {
        string html = "<div class=\"section-card\"><div class=\"section-header\"><h2>Title</h2></div><div class=\"section-body\"><p>Content</p></div></div>";

        int opens = CountPattern(html, @"<div[\s>]");
        int closes = CountPattern(html, @"</div>");

        Assert.Equal(opens, closes);
    }

    #endregion

    #region Test 7: Validate the builder output itself

    [Fact]
    public void SectionTable_Render_ProducesWellFormedHtml()
    {
        // This tests the CSectionTable builder output
        // Using a simple test data class
        var table = new VeeamHealthCheck.Functions.Reporting.Html.Shared.CSectionTable<TestItem>("test-id", "Test Section")
            .Column("Name", "The name", item => item.Name, leftAlign: true)
            .Column("Value", "The value", item => item.Value);

        var data = new[]
        {
            new TestItem { Name = "Item1", Value = "10" },
            new TestItem { Name = "Item2", Value = "20" }
        };

        string html = table.Render(data);

        // Validate structure
        Assert.Contains("<table>", html);
        Assert.Contains("</table>", html);
        Assert.Contains("<thead>", html);
        Assert.Contains("</thead>", html);
        Assert.Contains("<tbody>", html);
        Assert.Contains("</tbody>", html);

        // No double <tr>
        Assert.DoesNotMatch(@"<tr>\s*<tr>", html);

        // Table open/close match
        Assert.Equal(CountPattern(html, @"<table[\s>]"), CountPattern(html, @"</table>"));

        // Div open/close match
        Assert.Equal(CountPattern(html, @"<div[\s>]"), CountPattern(html, @"</div>"));

        // Correct column count in thead
        Assert.Equal(2, CountPattern(html, @"<th "));

        // Correct row count in tbody (2 data rows)
        int trInTbody = CountPattern(html, @"<tr>");
        Assert.True(trInTbody >= 3, "Should have header row + 2 data rows");
    }

    [Fact]
    public void SectionTable_RenderEmpty_ProducesWellFormedHtml()
    {
        var table = new VeeamHealthCheck.Functions.Reporting.Html.Shared.CSectionTable<TestItem>("empty-test", "Empty Section");

        string html = table.Render(Array.Empty<TestItem>());

        // Should have table structure even when empty
        Assert.Contains("<table>", html);
        Assert.Contains("</table>", html);
        Assert.Contains("<thead>", html);
        Assert.Contains("<tbody>", html);

        // Balanced divs
        Assert.Equal(CountPattern(html, @"<div[\s>]"), CountPattern(html, @"</div>"));
    }

    [Fact]
    public void SectionTable_WithIcon_IncludesIconSpan()
    {
        var table = new VeeamHealthCheck.Functions.Reporting.Html.Shared.CSectionTable<TestItem>("icon-test", "Icon Section")
            .WithIcon("T", "#faf5ff", "#7c3aed")
            .Column("Name", "", item => item.Name);

        string html = table.Render(new[] { new TestItem { Name = "A", Value = "1" } });

        Assert.Contains("class=\"icon\"", html);
        Assert.Contains("#faf5ff", html);
        Assert.Contains("#7c3aed", html);
    }

    [Fact]
    public void SectionTable_WithInterval_AppendsDaysToTitle()
    {
        var table = new VeeamHealthCheck.Functions.Reporting.Html.Shared.CSectionTable<TestItem>("interval-test", "Sessions")
            .WithInterval(7)
            .Column("Name", "", item => item.Name);

        string html = table.Render(new[] { new TestItem { Name = "A", Value = "1" } });

        Assert.Contains("(7 Days)", html);
    }

    [Fact]
    public void SectionTable_BoolColumn_RendersCheckboxEmoji()
    {
        var table = new VeeamHealthCheck.Functions.Reporting.Html.Shared.CSectionTable<TestItem>("bool-test", "Bool Section")
            .Column("Enabled", "", item => item.Name == "True");

        var data = new[]
        {
            new TestItem { Name = "True", Value = "1" },
            new TestItem { Name = "False", Value = "0" }
        };

        string html = table.Render(data);

        // Should contain both true and false emoji
        Assert.Contains("&#9989;", html);   // checkmark
        Assert.Contains("&#9744;", html);   // empty checkbox
    }

    #endregion

    #region CHtmlBuilder Tests

    [Fact]
    public void CHtmlBuilder_Build_ProducesBalancedTags()
    {
        var builder = new VeeamHealthCheck.Functions.Reporting.Html.Shared.CHtmlBuilder();
        string html = builder
            .OpenTag("div", ("class", "container"))
            .OpenTag("p")
            .Text("Hello")
            .CloseTag("p")
            .CloseTag("div")
            .Build();

        Assert.Equal("<div class=\"container\"><p>Hello</p></div>", html);
    }

    [Fact]
    public void CHtmlBuilder_Build_ThrowsOnUnbalancedTags()
    {
        var builder = new VeeamHealthCheck.Functions.Reporting.Html.Shared.CHtmlBuilder();
        builder.OpenTag("div");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void CHtmlBuilder_CloseTag_ThrowsOnMismatch()
    {
        var builder = new VeeamHealthCheck.Functions.Reporting.Html.Shared.CHtmlBuilder();
        builder.OpenTag("div");

        Assert.Throws<InvalidOperationException>(() => builder.CloseTag("span"));
    }

    [Fact]
    public void CHtmlBuilder_Text_EncodesHtmlEntities()
    {
        var builder = new VeeamHealthCheck.Functions.Reporting.Html.Shared.CHtmlBuilder();
        string html = builder
            .OpenTag("p")
            .Text("<script>alert('xss')</script>")
            .CloseTag("p")
            .Build();

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    #endregion

    #region Test Data

    private static string ExtractTagContent(string html, string tagName)
    {
        var match = Regex.Match(html, $@"<{tagName}[^>]*>(.*?)</{tagName}>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    #endregion
}

/// <summary>
/// Simple test data class for CSectionTable tests.
/// </summary>
internal class TestItem
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}
