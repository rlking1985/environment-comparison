using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EnvironmentComparison.Domain;

namespace EnvironmentComparison.Services
{
    public sealed class ComparisonOutputFilterService
    {
        public IReadOnlyList<ComparisonIssue> FilterIssuesByTableLogicalName(
            IEnumerable<ComparisonIssue> issues,
            Regex tableLogicalNameRegex)
        {
            if (issues == null) throw new ArgumentNullException(nameof(issues));
            if (tableLogicalNameRegex == null) throw new ArgumentNullException(nameof(tableLogicalNameRegex));

            return issues
                .Where(issue => IncludeTableScopedItem(issue.TableLogicalName, tableLogicalNameRegex))
                .ToList();
        }

        public MetadataComparisonResult FilterByTableLogicalName(
            MetadataComparisonResult result,
            Regex tableLogicalNameRegex)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (tableLogicalNameRegex == null) throw new ArgumentNullException(nameof(tableLogicalNameRegex));

            return new MetadataComparisonResult(
                FilterSnapshot(result.EnvironmentA, tableLogicalNameRegex),
                FilterSnapshot(result.EnvironmentB, tableLogicalNameRegex),
                FilterIssuesByTableLogicalName(result.Issues, tableLogicalNameRegex));
        }

        private static EnvironmentMetadataSnapshot FilterSnapshot(
            EnvironmentMetadataSnapshot snapshot,
            Regex tableLogicalNameRegex)
        {
            return new EnvironmentMetadataSnapshot(
                snapshot.Tables.Where(table => tableLogicalNameRegex.IsMatch(table.LogicalName)),
                snapshot.Forms.Where(form => IncludeTableScopedItem(form.TableLogicalName, tableLogicalNameRegex)),
                snapshot.Views.Where(view => IncludeTableScopedItem(view.TableLogicalName, tableLogicalNameRegex)),
                snapshot.IncludedAreas,
                snapshot.IncludesUnpublishedMetadata,
                snapshot.Reports,
                snapshot.Processes.Where(process => IncludeTableScopedItem(process.TableLogicalName, tableLogicalNameRegex)));
        }

        private static bool IncludeTableScopedItem(string tableLogicalName, Regex tableLogicalNameRegex)
        {
            return string.IsNullOrWhiteSpace(tableLogicalName)
                || tableLogicalNameRegex.IsMatch(tableLogicalName);
        }
    }
}
