using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EnvironmentComparison.Domain;
using EnvironmentComparison.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EnvironmentComparison.Tests
{
    [TestClass]
    public sealed class ComparisonOutputFilterServiceTests
    {
        [TestMethod]
        public void FiltersTableComponentsButRetainsReportsAndOrganizationLevelRows()
        {
            var snapshot = new EnvironmentMetadataSnapshot(
                new[] { Table("ata_student"), Table("account") },
                new[]
                {
                    Form("ata_student|form:1", "ata_student"),
                    Form("account|form:1", "account")
                },
                new[]
                {
                    View("ata_student|view:1", "ata_student"),
                    View("account|view:1", "account")
                },
                ComparisonAreas.TableMetadata | ComparisonAreas.Columns | ComparisonAreas.Forms | ComparisonAreas.Views | ComparisonAreas.Reports | ComparisonAreas.CloudFlows | ComparisonAreas.BusinessRules,
                true,
                new[] { new ReportMetadataInfo("report|id:1", "Report", new Dictionary<string, string>()) },
                new[]
                {
                    Process(ComparisonScope.BusinessRule, "account", "BusinessRule|id:1"),
                    Process(ComparisonScope.CloudFlow, string.Empty, "CloudFlow|id:1")
                });
            var issues = new[]
            {
                Issue(ComparisonScope.Column, "ata_student", "ata_student|ata_name"),
                Issue(ComparisonScope.Form, "account", "account|form:1"),
                Issue(ComparisonScope.Report, string.Empty, "report|id:1"),
                Issue(ComparisonScope.BusinessRule, "account", "BusinessRule|id:1"),
                Issue(ComparisonScope.CloudFlow, string.Empty, "CloudFlow|id:1")
            };
            var result = new MetadataComparisonResult(snapshot, snapshot, issues);
            var regex = new Regex("^(ata_|mshied_)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));

            var filtered = new ComparisonOutputFilterService().FilterByTableLogicalName(result, regex);

            CollectionAssert.AreEqual(new[] { "ata_student" }, filtered.EnvironmentA.Tables.Select(table => table.LogicalName).ToArray());
            CollectionAssert.AreEqual(new[] { "ata_student" }, filtered.EnvironmentA.Forms.Select(form => form.TableLogicalName).ToArray());
            CollectionAssert.AreEqual(new[] { "ata_student" }, filtered.EnvironmentA.Views.Select(view => view.TableLogicalName).ToArray());
            Assert.AreEqual(1, filtered.EnvironmentA.Reports.Count);
            Assert.AreEqual(1, filtered.EnvironmentA.Processes.Count);
            Assert.AreEqual(ComparisonScope.CloudFlow, filtered.EnvironmentA.Processes.Single().Scope);
            Assert.AreEqual(3, filtered.Issues.Count);
            Assert.IsTrue(filtered.Issues.Any(issue => issue.Scope == ComparisonScope.Column));
            Assert.IsTrue(filtered.Issues.Any(issue => issue.Scope == ComparisonScope.Report));
            Assert.IsTrue(filtered.Issues.Any(issue => issue.Scope == ComparisonScope.CloudFlow));
            Assert.AreEqual(snapshot.IncludedAreas, filtered.EnvironmentA.IncludedAreas);
            Assert.IsTrue(filtered.EnvironmentA.IncludesUnpublishedMetadata);
        }

        private static TableMetadataInfo Table(string logicalName)
        {
            return new TableMetadataInfo(logicalName, new Dictionary<string, string>(), Array.Empty<ColumnMetadataInfo>());
        }

        private static FormMetadataInfo Form(string key, string tableLogicalName)
        {
            return new FormMetadataInfo(key, tableLogicalName, key, new Dictionary<string, string>());
        }

        private static ViewMetadataInfo View(string key, string tableLogicalName)
        {
            return new ViewMetadataInfo(key, tableLogicalName, key, new Dictionary<string, string>());
        }

        private static ProcessMetadataInfo Process(ComparisonScope scope, string tableLogicalName, string key)
        {
            return new ProcessMetadataInfo(key, scope, tableLogicalName, key, new Dictionary<string, string>());
        }

        private static ComparisonIssue Issue(ComparisonScope scope, string tableLogicalName, string componentKey)
        {
            return new ComparisonIssue(
                DifferenceSeverity.High,
                scope,
                DifferenceKind.Changed,
                tableLogicalName,
                tableLogicalName,
                componentKey,
                componentKey,
                "Property",
                "A",
                "B",
                "Different");
        }
    }
}
