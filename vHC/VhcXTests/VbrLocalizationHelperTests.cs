using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using VeeamHealthCheck.Resources.Localization;
using Xunit;

namespace VhcXTests
{
    public class VbrLocalizationHelperTests
    {
        // These exact casings are also ordinal lookup keys into locale-known-missing.txt
        // and KnownDeliberateOrphans below. CultureInfo resolves them case-insensitively,
        // so "normalizing" the casing here (e.g. "fR-FR" -> "fr-FR") would still load the
        // right satellite but silently stop matching every allowlist entry for that
        // culture, failing loudly with dozens of spurious missing/orphan reports whose
        // real cause (a casing change) wouldn't be obvious from the failure message.
        private static readonly string[] SatelliteCultures = { "fR-FR", "ja", "zh-CN", "zh-tw" };

        // Pre-existing neutral resx keys whose <value> is genuinely empty - not
        // Stage D's concern, and not a missing/typo'd key. Confirmed by direct
        // inspection of vhcres.resx before this stage touched it. A key belongs
        // here only if its blank value is a pre-existing fact, not a new typo -
        // do not add a Stage D key to this list to make a failing test pass.
        private static readonly HashSet<string> KnownEmptyNeutralKeys = new()
        {
            "Prx10TT",
            "JobCon1TT", "JobCon2TT", "JobCon3TT", "JobCon4TT", "JobCon5TT", "JobCon6TT", "JobCon7TT",
            "TaskCon1TT", "TaskCon2TT", "TaskCon3TT", "TaskCon4TT", "TaskCon5TT", "TaskCon6TT", "TaskCon7TT",
            "Reg0TT", "Reg1TT",
            "JobInfo6TT", "JobInfo7TT", "JobInfo8TT", "JobInfo9TT",
            "JobInfo10TT", "JobInfo11TT", "JobInfo12TT", "JobInfo13TT",
        };

        // HtmlIntroLine3 is deliberately left orphaned in all four satellites (commit
        // 39be2be5). Its translated values predate the neutral key's split into
        // HtmlIntroLine3Anon/HtmlIntroLine3Original and are malformed HTML (no closing
        // </a>; zh-tw's is a truncated tag fragment) pointing at the pre-split
        // JobSessionReports path. Renaming them onto the live key emits broken HTML into
        // the report (CHtmlCompiler.cs interpolates the value into <dd>{1}</dd> with no
        // closing tag appended). They are kept under the old key name so a translator can
        // produce well-formed replacements later. DO NOT delete these keys or rename them
        // to make this test pass - either "fix" reintroduces the bug 39be2be5 resolved,
        // or destroys real translated content.
        private static readonly HashSet<(string Culture, string Key)> KnownDeliberateOrphans = new()
        {
            ("fR-FR", "HtmlIntroLine3"), ("ja", "HtmlIntroLine3"),
            ("zh-CN", "HtmlIntroLine3"), ("zh-tw", "HtmlIntroLine3"),
        };

        [Fact]
        public void AllStaticStrings_ResolveNonNullAndNonEmpty()
        {
            var fields = typeof(VbrLocalizationHelper)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string));

            var blank = new List<string>();
            foreach (var field in fields)
            {
                var value = (string)field.GetValue(null);
                if (string.IsNullOrEmpty(value) && !KnownEmptyNeutralKeys.Contains(field.Name))
                {
                    blank.Add(field.Name);
                }
            }

            Assert.True(blank.Count == 0,
                "Fields resolving null/empty under the neutral resx (missing or typo'd key, not in KnownEmptyNeutralKeys): " + string.Join(", ", blank));
        }

        [Fact]
        public void EverySatellite_HasNoOrphansAndNoUnexpectedMissingKeys()
        {
            var resourceManager = new ResourceManager(
                "VeeamHealthCheck.Resources.Localization.vhcres",
                typeof(VbrLocalizationHelper).Assembly);

            var neutralKeys = GetKeySet(resourceManager, CultureInfo.InvariantCulture);
            var knownMissing = LoadKnownMissing();

            foreach (var culture in SatelliteCultures)
            {
                var satelliteKeys = GetKeySet(resourceManager, new CultureInfo(culture));

                var orphans = satelliteKeys.Except(neutralKeys)
                    .Where(k => !KnownDeliberateOrphans.Contains((culture, k)))
                    .ToList();
                Assert.True(orphans.Count == 0,
                    $"{culture} has {orphans.Count} orphan key(s) absent from the neutral resx and not in KnownDeliberateOrphans: {string.Join(", ", orphans)}");

                var missing = neutralKeys.Except(satelliteKeys)
                    .Where(k => !knownMissing.Contains((culture, k)))
                    .ToList();
                Assert.True(missing.Count == 0,
                    $"{culture} is missing {missing.Count} key(s) not covered by locale-known-missing.txt: {string.Join(", ", missing)}");
            }
        }

        private static HashSet<string> GetKeySet(ResourceManager resourceManager, CultureInfo culture)
        {
            var set = resourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
            var keys = new HashSet<string>();
            if (set != null)
            {
                foreach (DictionaryEntry entry in set)
                {
                    keys.Add((string)entry.Key);
                }
            }
            return keys;
        }

        private static HashSet<(string Culture, string Key)> LoadKnownMissing()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Localization", "locale-known-missing.txt");
            var set = new HashSet<(string, string)>();
            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                var parts = line.Split(':', 2);
                set.Add((parts[0], parts[1]));
            }
            return set;
        }
    }
}
