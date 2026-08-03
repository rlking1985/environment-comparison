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
            var missingColumn = result.Issues.Single(issue => issue.Scope == ComparisonScope.Column
                && issue.ComponentKey == "new_code");
            Assert.AreEqual("Code (new_code)", missingColumn.EnvironmentAComponent);
            Assert.AreEqual(string.Empty, missingColumn.EnvironmentBComponent);
        }

        [TestMethod]
        public void ReportsImportantColumnSettingsAndDisplayNameRenames()
        {
            var sourceColumn = Column("new_code", "Student code", "String", "Metadata ID", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Requirement level", "ApplicationRequired", "Maximum length", "100", "Audit enabled", "True");
            var targetColumn = Column("new_code", "Learner code", "String", "Metadata ID", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "Requirement level", "None", "Maximum length", "50", "Audit enabled", "False");

            var result = _service.Compare(
                Snapshot(new[] { Table("new_student", sourceColumn) }, ComparisonAreas.Columns),
                Snapshot(new[] { Table("new_student", targetColumn) }, ComparisonAreas.Columns));

            CollectionAssert.IsSubsetOf(
                new[] { "Display name", "Requirement level", "Maximum length", "Audit enabled" },
                result.Issues.Select(issue => issue.PropertyName).ToArray());
            Assert.AreEqual(DifferenceSeverity.Critical, result.Issues.Single(issue => issue.PropertyName == "Requirement level").Severity);
            StringAssert.Contains(result.Issues.Single(issue => issue.PropertyName == "Display name").Details, "renamed");
            var maximumLengthIssue = result.Issues.Single(issue => issue.PropertyName == "Maximum length");
            Assert.AreEqual("Student code (new_code)", maximumLengthIssue.EnvironmentAComponent);
            Assert.AreEqual("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", maximumLengthIssue.EnvironmentAComponentId);
            Assert.AreEqual("Learner code (new_code)", maximumLengthIssue.EnvironmentBComponent);
            Assert.AreEqual("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", maximumLengthIssue.EnvironmentBComponentId);
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
                "Form ID", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                "Form XML", DataverseMetadataService.NormalizeDefinition("<form><tab id='a'/></form>"),
                "Form type", "2"));
            var formB = new FormMetadataInfo("new_student|unique:main", "new_student", "Student form", Properties(
                "Form ID", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                "Form XML", DataverseMetadataService.NormalizeDefinition("<form><tab id='b'/></form>"),
                "Form type", "2"));
            var viewA = new ViewMetadataInfo("new_student|id:1", "new_student", "Active students", Properties(
                "View ID", "cccccccc-cccc-cccc-cccc-cccccccccccc",
                "Fetch XML", DataverseMetadataService.NormalizeDefinition("<fetch><entity name='new_student'/></fetch>"),
                "Layout XML", DataverseMetadataService.NormalizeDefinition("<grid name='resultset'/>") ));
            var viewB = new ViewMetadataInfo("new_student|id:1", "new_student", "Current students", Properties(
                "View ID", "dddddddd-dddd-dddd-dddd-dddddddddddd",
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
            Assert.AreEqual("Main form", formIssue.EnvironmentAComponent);
            Assert.AreEqual("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", formIssue.EnvironmentAComponentId);
            Assert.AreEqual("Student form", formIssue.EnvironmentBComponent);
            Assert.AreEqual("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", formIssue.EnvironmentBComponentId);
            StringAssert.StartsWith(viewIssue.EnvironmentAValue, "<fetch");
            StringAssert.StartsWith(viewIssue.EnvironmentAPreviewValue, "SHA-256 ");
            Assert.AreEqual("Active students", viewIssue.EnvironmentAComponent);
            Assert.AreEqual("cccccccc-cccc-cccc-cccc-cccccccccccc", viewIssue.EnvironmentAComponentId);
            Assert.AreEqual("Current students", viewIssue.EnvironmentBComponent);
            Assert.AreEqual("dddddddd-dddd-dddd-dddd-dddddddddddd", viewIssue.EnvironmentBComponentId);
            Assert.IsFalse(result.Issues.Any(issue => issue.Scope == ComparisonScope.Table || issue.Scope == ComparisonScope.Column));
        }

        [TestMethod]
        public void MatchesFormsWithDuplicateUniqueNamesByTypeAndPresentation()
        {
            var table = Table("new_student");
            var formsA = new[]
            {
                Form("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "shared", "2", "1", "Main form"),
                Form("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "shared", "7", "1", "Quick create form")
            };
            var formsB = new[]
            {
                Form("cccccccc-cccc-cccc-cccc-cccccccccccc", "shared", "2", "1", "Main form"),
                Form("dddddddd-dddd-dddd-dddd-dddddddddddd", "shared", "7", "1", "Quick create form")
            };

            var result = _service.Compare(
                new EnvironmentMetadataSnapshot(new[] { table }, formsA, includedAreas: ComparisonAreas.Forms),
                new EnvironmentMetadataSnapshot(new[] { table }, formsB, includedAreas: ComparisonAreas.Forms));

            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void ReportsAmbiguousFormIdentityInsteadOfThrowing()
        {
            var table = Table("new_student");
            var formsA = new[]
            {
                Form("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "shared", "2", "1", "Main form A1"),
                Form("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "shared", "2", "1", "Main form A2")
            };
            var formsB = new[]
            {
                Form("cccccccc-cccc-cccc-cccc-cccccccccccc", "shared", "2", "1", "Main form B")
            };

            var result = _service.Compare(
                new EnvironmentMetadataSnapshot(new[] { table }, formsA, includedAreas: ComparisonAreas.Forms),
                new EnvironmentMetadataSnapshot(new[] { table }, formsB, includedAreas: ComparisonAreas.Forms));

            var issue = result.Issues.Single();
            Assert.AreEqual("Form identity", issue.PropertyName);
            StringAssert.Contains(issue.Details, "one-to-one fallback match could not be selected");
            StringAssert.Contains(issue.EnvironmentAComponentId, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            StringAssert.Contains(issue.EnvironmentAComponentId, "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            Assert.AreEqual("cccccccc-cccc-cccc-cccc-cccccccccccc", issue.EnvironmentBComponentId);
        }

        [TestMethod]
        public void PrefersUnpublishedFormWhenDuplicateFormIdsAreReturned()
        {
            var table = Table("new_student");
            var formId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            var published = Form(formId, "main", "2", "1", "Main form", "0", "<form version='published' />");
            var unpublished = Form(formId, "main", "2", "1", "Main form", "1", "<form version='unpublished' />");

            var result = _service.Compare(
                new EnvironmentMetadataSnapshot(new[] { table }, new[] { published, unpublished }, includedAreas: ComparisonAreas.Forms, includesUnpublishedMetadata: true),
                new EnvironmentMetadataSnapshot(new[] { table }, new[] { unpublished }, includedAreas: ComparisonAreas.Forms, includesUnpublishedMetadata: true));

            Assert.AreEqual(0, result.Issues.Count);
        }

        [TestMethod]
        public void ReportsDifferentCustomFormSecurityRolesSeparatelyFromFormXml()
        {
            var table = Table("new_student");
            var formA = new FormMetadataInfo(
                "new_student|unique:main",
                table.LogicalName,
                "Main form",
                Properties(
                    "Form XML", "<form><DisplayConditions FallbackForm=\"true\" Order=\"1\" /></form>",
                    "Form security role identities", "root:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                    "Form security roles", "Student Administrator [root:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa]"));
            var formB = new FormMetadataInfo(
                "new_student|unique:main",
                table.LogicalName,
                "Main form",
                Properties(
                    "Form XML", "<form><DisplayConditions FallbackForm=\"true\" Order=\"1\" /></form>",
                    "Form security role identities", "root:bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                    "Form security roles", "Student Administrator [root:bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb]"));

            var result = _service.Compare(
                new EnvironmentMetadataSnapshot(new[] { table }, new[] { formA }, includedAreas: ComparisonAreas.Forms),
                new EnvironmentMetadataSnapshot(new[] { table }, new[] { formB }, includedAreas: ComparisonAreas.Forms));
            var issue = result.Issues.Single(item => item.PropertyName == "Form security roles");

            Assert.AreEqual(DifferenceSeverity.Critical, issue.Severity);
            Assert.AreEqual("Student Administrator [root:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa]", issue.EnvironmentAValue);
            Assert.AreEqual("Student Administrator [root:bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb]", issue.EnvironmentBValue);
            Assert.IsFalse(result.Issues.Any(item => item.PropertyName == "Form XML"));
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
        public void ComparesSsrsReportRdlAndPublicationMetadata()
        {
            var reportA = new ReportMetadataInfo(
                "report|id:12345678-aaaa-bbbb-cccc-1234567890ab",
                "Student summary",
                Properties(
                    "Name", "Student summary",
                    "Associated tables", "account | contact",
                    "Categories", "4",
                    "Visibility", "1 | 2",
                    "RDL", DataverseMetadataService.NormalizeDefinition(
                        "<Report xmlns='urn:report'><DataSets><DataSet Name='Students' /></DataSets></Report>")));
            var reportB = new ReportMetadataInfo(
                reportA.Key,
                "Student summary",
                Properties(
                    "Name", "Student summary",
                    "Associated tables", "account",
                    "Categories", "4",
                    "Visibility", "1 | 2",
                    "RDL", DataverseMetadataService.NormalizeDefinition(
                        "<Report xmlns='urn:report'><DataSets><DataSet Name='Enrolments' /></DataSets></Report>")));

            var result = _service.Compare(
                new EnvironmentMetadataSnapshot(
                    Array.Empty<TableMetadataInfo>(),
                    includedAreas: ComparisonAreas.Reports,
                    reports: new[] { reportA }),
                new EnvironmentMetadataSnapshot(
                    Array.Empty<TableMetadataInfo>(),
                    includedAreas: ComparisonAreas.Reports,
                    reports: new[] { reportB }));

            var rdl = result.Issues.Single(issue => issue.PropertyName == "RDL");
            var tables = result.Issues.Single(issue => issue.PropertyName == "Associated tables");
            Assert.AreEqual(ComparisonScope.Report, rdl.Scope);
            Assert.AreEqual(DifferenceSeverity.Critical, rdl.Severity);
            StringAssert.StartsWith(rdl.EnvironmentAPreviewValue, "SHA-256 ");
            Assert.AreEqual(DifferenceSeverity.Critical, tables.Severity);
            Assert.AreEqual("account | contact", tables.EnvironmentAValue);
            Assert.AreEqual("account", tables.EnvironmentBValue);
        }

        [TestMethod]
        public void IgnoresFormattingOnlySsrsRdlDifferences()
        {
            var reportA = new ReportMetadataInfo(
                "report|id:12345678-aaaa-bbbb-cccc-1234567890ab",
                "Student summary",
                Properties(
                    "Name", "Student summary",
                    "RDL", DataverseMetadataService.NormalizeDefinition(
                        "<Report xmlns='urn:report'><DataSets><DataSet Name='Students' /></DataSets></Report>")));
            var reportB = new ReportMetadataInfo(
                reportA.Key,
                "Student summary",
                Properties(
                    "Name", "Student summary",
                    "RDL", DataverseMetadataService.NormalizeDefinition(
                        "<Report xmlns='urn:report'>\r\n  <DataSets>\r\n    <DataSet Name='Students'></DataSet>\r\n  </DataSets>\r\n</Report>")));

            var result = _service.Compare(
                new EnvironmentMetadataSnapshot(
                    Array.Empty<TableMetadataInfo>(),
                    includedAreas: ComparisonAreas.Reports,
                    reports: new[] { reportA }),
                new EnvironmentMetadataSnapshot(
                    Array.Empty<TableMetadataInfo>(),
                    includedAreas: ComparisonAreas.Reports,
                    reports: new[] { reportB }));

            Assert.IsFalse(result.Issues.Any());
        }

        [TestMethod]
        public void FallbackMatchesUniqueReportsWithDifferentIdsAndExplainsEveryDifference()
        {
            var reportA = Report(
                "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                "Account Summary",
                "Account Summary.rdl",
                "Description", "Environment A description");
            var reportB = Report(
                "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                "Account Summary",
                "Account Summary.rdl",
                "Description", "Environment B description");

            var result = _service.Compare(
                new EnvironmentMetadataSnapshot(Array.Empty<TableMetadataInfo>(), includedAreas: ComparisonAreas.Reports, reports: new[] { reportA }),
                new EnvironmentMetadataSnapshot(Array.Empty<TableMetadataInfo>(), includedAreas: ComparisonAreas.Reports, reports: new[] { reportB }));

            Assert.IsFalse(result.Issues.Any(issue => issue.Kind != DifferenceKind.Changed));
            CollectionAssert.AreEquivalent(
                new[] { "Description", "Report ID" },
                result.Issues.Select(issue => issue.PropertyName).ToArray());
            Assert.IsTrue(result.Issues.All(issue => issue.ComponentKey == reportA.Key));
            Assert.IsTrue(result.Issues.All(issue => issue.EnvironmentAComponentKey == reportA.Key));
            Assert.IsTrue(result.Issues.All(issue => issue.EnvironmentBComponentKey == reportB.Key));
            Assert.IsTrue(result.Issues.All(issue => issue.EnvironmentAComponent == "Account Summary"));
            Assert.IsTrue(result.Issues.All(issue => issue.EnvironmentAComponentId == "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
            Assert.IsTrue(result.Issues.All(issue => issue.EnvironmentBComponent == "Account Summary"));
            Assert.IsTrue(result.Issues.All(issue => issue.EnvironmentBComponentId == "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
            Assert.IsTrue(result.Issues.All(issue => issue.Details.Contains("Fallback matched by report name, filename, report type and language")));
            Assert.IsTrue(result.Issues.All(issue => issue.Details.Contains("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
            Assert.IsTrue(result.Issues.All(issue => issue.Details.Contains("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")));
        }

        [TestMethod]
        public void MatchesExactReportIdBeforeConsideringDuplicateFallbackIdentity()
        {
            var current = Report(
                "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                "Student Medical Summary Report Base",
                "Student Medical Summary Report Base.rdl");
            var legacy = Report(
                "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                "Student Medical Summary Report Base",
                "Student Medical Summary Report Base.rdl");

            var result = _service.Compare(
                new EnvironmentMetadataSnapshot(Array.Empty<TableMetadataInfo>(), includedAreas: ComparisonAreas.Reports, reports: new[] { current, legacy }),
                new EnvironmentMetadataSnapshot(Array.Empty<TableMetadataInfo>(), includedAreas: ComparisonAreas.Reports, reports: new[] { current }));

            var issue = result.Issues.Single();
            Assert.AreEqual(DifferenceKind.MissingInEnvironmentB, issue.Kind);
            Assert.AreEqual(legacy.Key, issue.ComponentKey);
            Assert.IsFalse(issue.Details.Contains("Fallback matched"));
        }

        [TestMethod]
        public void DoesNotFallbackMatchAmbiguousDuplicateReports()
        {
            var reportsA = new[]
            {
                Report("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Duplicate", "Duplicate.rdl"),
                Report("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "Duplicate", "Duplicate.rdl")
            };
            var reportsB = new[]
            {
                Report("cccccccc-cccc-cccc-cccc-cccccccccccc", "Duplicate", "Duplicate.rdl"),
                Report("dddddddd-dddd-dddd-dddd-dddddddddddd", "Duplicate", "Duplicate.rdl")
            };

            var result = _service.Compare(
                new EnvironmentMetadataSnapshot(Array.Empty<TableMetadataInfo>(), includedAreas: ComparisonAreas.Reports, reports: reportsA),
                new EnvironmentMetadataSnapshot(Array.Empty<TableMetadataInfo>(), includedAreas: ComparisonAreas.Reports, reports: reportsB));

            Assert.AreEqual(4, result.Issues.Count);
            Assert.IsTrue(result.Issues.All(issue => issue.PropertyName == "Report presence"));
            Assert.IsTrue(result.Issues.All(issue => issue.Details.Contains("fallback match was not used")));
            Assert.IsTrue(result.Issues.All(issue => issue.Details.Contains("2 candidate(s)")));
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
                Properties("Metadata ID", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Display name", "Process", "Table classification", "Standard"),
                Array.Empty<ColumnMetadataInfo>());
            var tableB = new TableMetadataInfo(
                "new_process",
                Properties("Metadata ID", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "Display name", "Business Process", "Table classification", "BPF"),
                Array.Empty<ColumnMetadataInfo>());

            var result = _service.Compare(
                Snapshot(new[] { tableA }, ComparisonAreas.TableMetadata),
                Snapshot(new[] { tableB }, ComparisonAreas.TableMetadata));
            var issue = result.Issues.Single(item => item.PropertyName == "Table classification");

            Assert.AreEqual(DifferenceSeverity.Critical, issue.Severity);
            Assert.AreEqual("Standard", issue.EnvironmentAValue);
            Assert.AreEqual("BPF", issue.EnvironmentBValue);
            Assert.AreEqual("Standard / BPF", issue.TableClassification);
            Assert.AreEqual("Process (new_process)", issue.EnvironmentAComponent);
            Assert.AreEqual("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", issue.EnvironmentAComponentId);
            Assert.AreEqual("Business Process (new_process)", issue.EnvironmentBComponent);
            Assert.AreEqual("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", issue.EnvironmentBComponentId);
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
                Properties("Display name", logicalName, "Schema name", logicalName, "Table classification", "Standard"),
                columns);
        }

        private static ColumnMetadataInfo Column(string logicalName, string displayName, string type, params string[] additionalProperties)
        {
            var properties = Properties("Display name", displayName, "Attribute type", type, "Requirement level", "None");
            for (var index = 0; index < additionalProperties.Length; index += 2)
            {
                properties[additionalProperties[index]] = additionalProperties[index + 1];
            }
            return new ColumnMetadataInfo(logicalName, properties);
        }

        private static FormMetadataInfo Form(
            string id,
            string uniqueName,
            string formType,
            string presentation,
            string name,
            string componentState = "0",
            string formXml = "<form />")
        {
            return new FormMetadataInfo(
                $"new_student|unique:{uniqueName}",
                "new_student",
                name,
                Properties(
                    "Form ID", id,
                    "Unique name", uniqueName,
                    "Name", name,
                    "Form type", formType,
                    "Presentation", presentation,
                    "Component state", componentState,
                    "Form XML", formXml));
        }

        private static ReportMetadataInfo Report(
            string id,
            string name,
            string fileName,
            params string[] additionalProperties)
        {
            var properties = Properties(
                "Report ID", id,
                "Name", name,
                "File name", fileName,
                "Report type", "1",
                "Language code", "1033",
                "RDL", "<Report />");
            for (var index = 0; index < additionalProperties.Length; index += 2)
            {
                properties[additionalProperties[index]] = additionalProperties[index + 1];
            }

            return new ReportMetadataInfo("report|id:" + id, name, properties);
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
