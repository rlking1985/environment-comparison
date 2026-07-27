using System;
using System.Collections.Generic;
using System.Linq;
using EnvironmentComparison.Domain;
using EnvironmentComparison.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EnvironmentComparison.Tests
{
    [TestClass]
    public sealed class MetadataComparisonServiceTests
    {
        private readonly MetadataComparisonService _service = new MetadataComparisonService();

        [TestMethod]
        public void ReportsMissingTablesAndColumnsInBothDirections()
        {
            var commonA = Table("new_student", Column("new_name", "Name", "String"), Column("new_code", "Code", "String"));
            var commonB = Table("new_student", Column("new_name", "Name", "String"), Column("new_extra", "Extra", "String"));
            var source = Snapshot(new[] { commonA, Table("new_sourceonly") }, ComparisonAreas.TableMetadata | ComparisonAreas.Columns);
            var target = Snapshot(new[] { commonB, Table("new_targetonly") }, ComparisonAreas.TableMetadata | ComparisonAreas.Columns);

            var result = _service.Compare(source, target);

            Assert.IsTrue(result.Issues.Any(issue => issue.Scope == ComparisonScope.Table
                && issue.TableLogicalName == "new_sourceonly"
                && issue.Kind == DifferenceKind.MissingInEnvironmentB));
            Assert.IsTrue(result.Issues.Any(issue => issue.Scope == ComparisonScope.Table
                && issue.TableLogicalName == "new_targetonly"
                && issue.Kind == DifferenceKind.MissingInEnvironmentA));
            Assert.IsTrue(result.Issues.Any(issue => issue.Scope == ComparisonScope.Column
                && issue.ComponentKey == "new_code"
                && issue.Kind == DifferenceKind.MissingInEnvironmentB));
            Assert.IsTrue(result.Issues.Any(issue => issue.Scope == ComparisonScope.Column
                && issue.ComponentKey == "new_extra"
                && issue.Kind == DifferenceKind.MissingInEnvironmentA));
        }

        [TestMethod]
        public void ReportsImportantColumnSettingsAndDisplayNameRenames()
        {
            var sourceColumn = Column("new_code", "Student code", "String", "Requirement level", "ApplicationRequired", "Maximum length", "100", "Audit enabled", "True");
            var targetColumn = Column("new_code", "Learner code", "String", "Requirement level", "None", "Maximum length", "50", "Audit enabled", "False");

            var result = _service.Compare(
                Snapshot(new[] { Table("new_student", sourceColumn) }, ComparisonAreas.Columns),
                Snapshot(new[] { Table("new_student", targetColumn) }, ComparisonAreas.Columns));

            CollectionAssert.IsSubsetOf(
                new[] { "Display name", "Requirement level", "Maximum length", "Audit enabled" },
                result.Issues.Select(issue => issue.PropertyName).ToArray());
            Assert.AreEqual(DifferenceSeverity.Critical, result.Issues.Single(issue => issue.PropertyName == "Requirement level").Severity);
            StringAssert.Contains(result.Issues.Single(issue => issue.PropertyName == "Display name").Details, "renamed");
        }

        [TestMethod]
        public void SuggestsPossibleRecreatedColumnWithoutTreatingItAsCertain()
        {
            var source = Snapshot(
                new[] { Table("new_student", Column("new_oldcode", "Student code", "String", "Maximum length", "25")) },
                ComparisonAreas.Columns);
            var target = Snapshot(
                new[] { Table("new_student", Column("new_newcode", "Student code", "String", "Maximum length", "25")) },
                ComparisonAreas.Columns);

            var result = _service.Compare(source, target);

            var missing = result.Issues.Single(issue => issue.ComponentKey == "new_oldcode");
            StringAssert.Contains(missing.Details, "Possible renamed/recreated column");
            StringAssert.Contains(missing.Details, "new_newcode");
        }

        [TestMethod]
        public void ComparesSelectedFormsAndViews()
        {
            var table = Table("new_student");
            var formA = new FormMetadataInfo("new_student|unique:main", "new_student", "Main form", Properties(
                "Form XML", DataverseMetadataService.NormalizeDefinition("<form><tab id='a'/></form>"),
                "Form type", "2"));
            var formB = new FormMetadataInfo("new_student|unique:main", "new_student", "Main form", Properties(
                "Form XML", DataverseMetadataService.NormalizeDefinition("<form><tab id='b'/></form>"),
                "Form type", "2"));
            var viewA = new ViewMetadataInfo("new_student|id:1", "new_student", "Active students", Properties(
                "Fetch XML", DataverseMetadataService.NormalizeDefinition("<fetch><entity name='new_student'/></fetch>"),
                "Layout XML", DataverseMetadataService.NormalizeDefinition("<grid name='resultset'/>") ));
            var viewB = new ViewMetadataInfo("new_student|id:1", "new_student", "Active students", Properties(
                "Fetch XML", DataverseMetadataService.NormalizeDefinition("<fetch><entity name='account'/></fetch>"),
                "Layout XML", DataverseMetadataService.NormalizeDefinition("<grid name='resultset'/>") ));
            var areas = ComparisonAreas.Forms | ComparisonAreas.Views;

            var result = _service.Compare(
                new EnvironmentMetadataSnapshot(new[] { table }, new[] { formA }, new[] { viewA }, areas),
                new EnvironmentMetadataSnapshot(new[] { table }, new[] { formB }, new[] { viewB }, areas));

            var formIssue = result.Issues.Single(issue => issue.Scope == ComparisonScope.Form && issue.PropertyName == "Form XML");
            var viewIssue = result.Issues.Single(issue => issue.Scope == ComparisonScope.View && issue.PropertyName == "Fetch XML");
            StringAssert.StartsWith(formIssue.EnvironmentAValue, "<form");
            StringAssert.StartsWith(formIssue.EnvironmentAPreviewValue, "SHA-256 ");
            Assert.AreEqual("Standard", formIssue.TableClassification);
            Assert.AreEqual("Yes", formIssue.CustomTable);
            Assert.AreEqual("Unknown", formIssue.CustomComponent);
            StringAssert.StartsWith(viewIssue.EnvironmentAValue, "<fetch");
            StringAssert.StartsWith(viewIssue.EnvironmentAPreviewValue, "SHA-256 ");
            Assert.IsFalse(result.Issues.Any(issue => issue.Scope == ComparisonScope.Table || issue.Scope == ComparisonScope.Column));
        }

        [TestMethod]
        public void IgnoresEnvironmentSpecificViewLayoutObjectTypeCodes()
        {
            var table = Table("ata_academicreportingcriteria");
            var layoutA = DataverseMetadataService.NormalizeDefinition(
                "<grid name='resultset' object='12801'><row id='ata_academicreportingcriteriaid'><cell name='ata_name' width='300'/></row></grid>");
            var layoutB = DataverseMetadataService.NormalizeDefinition(
                "<grid name='resultset' object='22657'><row id='ata_academicreportingcriteriaid'><cell name='ata_name' width='300'/></row></grid>");
            var viewA = new ViewMetadataInfo("ata_academicreportingcriteria|id:1", table.LogicalName, "Active criteria", Properties("Layout XML", layoutA));
            var viewB = new ViewMetadataInfo("ata_academicreportingcriteria|id:1", table.LogicalName, "Active criteria", Properties("Layout XML", layoutB));

            var result = _service.Compare(
                new EnvironmentMetadataSnapshot(new[] { table }, views: new[] { viewA }, includedAreas: ComparisonAreas.Views),
                new EnvironmentMetadataSnapshot(new[] { table }, views: new[] { viewB }, includedAreas: ComparisonAreas.Views));

            Assert.IsFalse(result.Issues.Any(issue => issue.PropertyName == "Layout XML"));
        }

        [TestMethod]
        public void ReportsRealViewLayoutChangesAndExportsOriginalObjectTypeCodes()
        {
            var table = Table("mshied_academicsubject");
            var layoutA = DataverseMetadataService.NormalizeDefinition(
                "<grid name='resultset' object='11609'><row id='mshied_academicsubjectid'><cell name='mshied_name' width='300'/><cell name='ata_schoolcampus' width='175'/></row></grid>");
            var layoutB = DataverseMetadataService.NormalizeDefinition(
                "<grid name='resultset' object='11358'><row id='mshied_academicsubjectid'><cell name='mshied_name' width='300'/></row></grid>");
            var viewA = new ViewMetadataInfo("mshied_academicsubject|id:1", table.LogicalName, "Active Academic Subjects", Properties("Layout XML", layoutA));
            var viewB = new ViewMetadataInfo("mshied_academicsubject|id:1", table.LogicalName, "Active Academic Subjects", Properties("Layout XML", layoutB));

            var result = _service.Compare(
                new EnvironmentMetadataSnapshot(new[] { table }, views: new[] { viewA }, includedAreas: ComparisonAreas.Views),
                new EnvironmentMetadataSnapshot(new[] { table }, views: new[] { viewB }, includedAreas: ComparisonAreas.Views));
            var issue = result.Issues.Single(item => item.PropertyName == "Layout XML");
            var csv = new CsvExportService().Create(new[] { issue });

            StringAssert.Contains(issue.EnvironmentAValue, "object=\"11609\"");
            StringAssert.Contains(issue.EnvironmentBValue, "object=\"11358\"");
            StringAssert.Contains(csv, "object=\"\"11609\"\"");
            StringAssert.Contains(csv, "object=\"\"11358\"\"");
        }

        [TestMethod]
        public void WhitespaceOnlyFormulaDefinitionsAreIgnored()
        {
            var formulaA = DataverseMetadataService.NormalizeDefinition(
                "<Activity><Sequence>\r\n  <Assign Value='Open Revenue' />\r\n</Sequence></Activity>");
            var formulaB = DataverseMetadataService.NormalizeDefinition(
                "<Activity>\n    <Sequence><Assign Value='Open Revenue'/></Sequence>\n</Activity>");
            var columnA = Column("openrevenue", "Open Revenue", "Money", "Formula definition", formulaA);
            var columnB = Column("openrevenue", "Open Revenue", "Money", "Formula definition", formulaB);

            var result = _service.Compare(
                Snapshot(new[] { Table("account", columnA) }, ComparisonAreas.Columns),
                Snapshot(new[] { Table("account", columnB) }, ComparisonAreas.Columns));

            Assert.IsFalse(result.Issues.Any(issue => issue.PropertyName == "Formula definition"));
        }

        [TestMethod]
        public void ReportsTableClassificationChanges()
        {
            var tableA = new TableMetadataInfo(
                "new_process",
                Properties("Display name", "Process", "Table classification", "Standard"),
                Array.Empty<ColumnMetadataInfo>());
            var tableB = new TableMetadataInfo(
                "new_process",
                Properties("Display name", "Process", "Table classification", "BPF"),
                Array.Empty<ColumnMetadataInfo>());

            var result = _service.Compare(
                Snapshot(new[] { tableA }, ComparisonAreas.TableMetadata),
                Snapshot(new[] { tableB }, ComparisonAreas.TableMetadata));
            var issue = result.Issues.Single(item => item.PropertyName == "Table classification");

            Assert.AreEqual(DifferenceSeverity.Critical, issue.Severity);
            Assert.AreEqual("Standard", issue.EnvironmentAValue);
            Assert.AreEqual("BPF", issue.EnvironmentBValue);
            Assert.AreEqual("Standard / BPF", issue.TableClassification);
        }

        [TestMethod]
        public void DoesNotCompareAreasThatWereNotSelected()
        {
            var tableA = new TableMetadataInfo("new_student", Properties("Display name", "Student"), new[] { Column("new_name", "Name", "String") });
            var tableB = new TableMetadataInfo("new_student", Properties("Display name", "Learner"), new[] { Column("new_name", "Name", "Integer") });

            var result = _service.Compare(
                Snapshot(new[] { tableA }, ComparisonAreas.TableMetadata),
                Snapshot(new[] { tableB }, ComparisonAreas.TableMetadata));

            Assert.IsTrue(result.Issues.Any(issue => issue.Scope == ComparisonScope.Table));
            Assert.IsFalse(result.Issues.Any(issue => issue.Scope == ComparisonScope.Column));
        }

        [TestMethod]
        public void IdenticalSnapshotsHaveNoIssues()
        {
            var table = Table("new_student", Column("new_name", "Name", "String"));
            var snapshot = Snapshot(new[] { table }, ComparisonAreas.All);

            Assert.AreEqual(0, _service.Compare(snapshot, snapshot).Issues.Count);
        }

        private static EnvironmentMetadataSnapshot Snapshot(IEnumerable<TableMetadataInfo> tables, ComparisonAreas areas)
        {
            return new EnvironmentMetadataSnapshot(tables, includedAreas: areas);
        }

        private static TableMetadataInfo Table(string logicalName, params ColumnMetadataInfo[] columns)
        {
            return new TableMetadataInfo(
                logicalName,
                Properties("Display name", logicalName, "Schema name", logicalName, "Table classification", "Standard", "Custom table", "Yes"),
                columns);
        }

        private static ColumnMetadataInfo Column(string logicalName, string displayName, string type, params string[] additionalProperties)
        {
            var properties = Properties("Display name", displayName, "Attribute type", type, "Requirement level", "None", "Custom component", "Yes");
            for (var index = 0; index < additionalProperties.Length; index += 2)
            {
                properties[additionalProperties[index]] = additionalProperties[index + 1];
            }
            return new ColumnMetadataInfo(logicalName, properties);
        }

        private static Dictionary<string, string> Properties(params string[] values)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < values.Length; index += 2)
            {
                result[values[index]] = values[index + 1];
            }
            return result;
        }
    }
}
