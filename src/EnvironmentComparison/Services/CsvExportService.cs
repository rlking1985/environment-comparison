using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EnvironmentComparison.Domain;

namespace EnvironmentComparison.Services
{
    public sealed class CsvExportService
    {
        // Excel accepts at most 32,767 characters in one cell. Leave headroom so
        // exports also remain safe when opened through Excel's CSV importer.
        internal const int ExcelSafeCellLength = 30000;

        private static readonly string[] LeadingHeaders =
        {
            "Severity",
            "Scope",
            "Difference",
            "Table logical name",
            "Table display name",
            "Table classification",
            "Component A",
            "Component A (ID)",
            "Component B",
            "Component B (ID)",
            "Property"
        };

        public string Create(IEnumerable<ComparisonIssue> issues)
        {
            if (issues == null) throw new ArgumentNullException(nameof(issues));

            var issueList = issues.ToList();
            var valuePartCount = Math.Max(
                1,
                issueList.SelectMany(issue => new[]
                    {
                        SplitForExcel(issue.EnvironmentAValue).Count,
                        SplitForExcel(issue.EnvironmentBValue).Count
                    })
                    .DefaultIfEmpty(1)
                    .Max());

            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",", CreateHeaders(valuePartCount).Select(Escape)));
            foreach (var issue in issueList)
            {
                var leadingValues = new[]
                {
                    issue.Severity.ToString(),
                    DisplayScope(issue.Scope),
                    DisplayKind(issue.Kind),
                    issue.TableLogicalName,
                    issue.TableDisplayName,
                    issue.TableClassification,
                    issue.EnvironmentAComponent,
                    issue.EnvironmentAComponentId,
                    issue.EnvironmentBComponent,
                    issue.EnvironmentBComponentId,
                    issue.PropertyName
                };
                var escapedValues = leadingValues.Select(Escape)
                    .Concat(EscapeParts(issue.EnvironmentAValue, valuePartCount))
                    .Concat(EscapeParts(issue.EnvironmentBValue, valuePartCount))
                    .Concat(new[] { Escape(issue.Details) });
                builder.AppendLine(string.Join(",", escapedValues));
            }

            return builder.ToString();
        }

        internal static string Escape(string? value)
        {
            return Escape(value, true);
        }

        internal static string Escape(string? value, bool neutralizeSpreadsheetFormula)
        {
            var safe = value ?? string.Empty;
            if (neutralizeSpreadsheetFormula
                && safe.Length > 0
                && (safe[0] == '=' || safe[0] == '+' || safe[0] == '-' || safe[0] == '@' || safe[0] == '\t'))
            {
                safe = "'" + safe;
            }

            return "\"" + safe.Replace("\"", "\"\"") + "\"";
        }

        internal static IReadOnlyList<string> SplitForExcel(string? value)
        {
            var text = value ?? string.Empty;
            if (text.Length == 0)
            {
                return new[] { string.Empty };
            }

            var parts = new List<string>(PartCount(text));
            for (var offset = 0; offset < text.Length;)
            {
                var length = Math.Min(ExcelSafeCellLength, text.Length - offset);
                if (length > 0
                    && offset + length < text.Length
                    && char.IsHighSurrogate(text[offset + length - 1])
                    && char.IsLowSurrogate(text[offset + length]))
                {
                    length--;
                }

                parts.Add(text.Substring(offset, length));
                offset += length;
            }

            return parts;
        }

        private static IEnumerable<string> CreateHeaders(int valuePartCount)
        {
            foreach (var header in LeadingHeaders)
            {
                yield return header;
            }

            foreach (var header in ValueHeaders("Environment A", valuePartCount))
            {
                yield return header;
            }

            foreach (var header in ValueHeaders("Environment B", valuePartCount))
            {
                yield return header;
            }

            yield return "Details";
        }

        private static IEnumerable<string> ValueHeaders(string environment, int valuePartCount)
        {
            if (valuePartCount == 1)
            {
                yield return environment;
                yield break;
            }

            for (var part = 1; part <= valuePartCount; part++)
            {
                yield return $"{environment} (part {part} of {valuePartCount})";
            }
        }

        private static IEnumerable<string> EscapeParts(string value, int valuePartCount)
        {
            var parts = SplitForExcel(value);
            for (var index = 0; index < valuePartCount; index++)
            {
                var part = index < parts.Count ? parts[index] : string.Empty;
                yield return Escape(part, index == 0);
            }
        }

        private static int PartCount(string? value)
        {
            var length = value?.Length ?? 0;
            return Math.Max(1, (length + ExcelSafeCellLength - 1) / ExcelSafeCellLength);
        }

        private static string DisplayKind(DifferenceKind kind)
        {
            switch (kind)
            {
                case DifferenceKind.MissingInEnvironmentA:
                    return "Missing in Environment A";
                case DifferenceKind.MissingInEnvironmentB:
                    return "Missing in Environment B";
                default:
                    return "Changed";
            }
        }

        private static string DisplayScope(ComparisonScope scope)
        {
            switch (scope)
            {
                case ComparisonScope.CloudFlow:
                    return "Cloud Flow";
                case ComparisonScope.BusinessRule:
                    return "Business Rule";
                default:
                    return scope.ToString();
            }
        }
    }
}
