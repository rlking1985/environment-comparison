using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnvironmentComparison.Domain;
using EnvironmentComparison.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EnvironmentComparison.Tests
{
    [TestClass]
    public sealed class HtmlExportServiceTests
    {
        [TestMethod]
        public void ExportsCompleteValuesInAPagedReportWithAutomaticallyLoadedPinnedCdnDiff()
        {
            var largeDefinition = "<Report>" + new string('x', 40000) + "</Report>";
            var issues = new[]
            {
                Issue(1, DifferenceSeverity.Critical, ComparisonScope.Report, "RDL", largeDefinition, "<Report />"),
                Issue(2, DifferenceSeverity.High, ComparisonScope.Column, "Requirement level", "ApplicationRequired", "None")
            };

            string html;
            int count;
            using (var writer = new StringWriter())
            {
                count = new HtmlExportService().Write(
                    writer,
                    issues,
                    "Scholarion Initial Build <A>",
                    "Atturra Tier 1 & Dev");
                html = writer.ToString();
            }

            Assert.AreEqual(2, count);
            StringAssert.StartsWith(html, "<!doctype html>");
            StringAssert.Contains(html, "id='comparison-data'");
            StringAssert.Contains(html, "id='environmentAName'");
            StringAssert.Contains(html, "id='environmentBName'");
            StringAssert.Contains(html, "\"environmentAName\":\"Scholarion Initial Build \\u003CA\\u003E\"");
            StringAssert.Contains(html, "\"environmentBName\":\"Atturra Tier 1 \\u0026 Dev\"");
            StringAssert.Contains(html, "document.title = `${environmentAName} vs ${environmentBName} - Dataverse environment comparison`");
            StringAssert.Contains(html, "Rows per page");
            StringAssert.Contains(html, "requestAnimationFrame(processChunk)");
            StringAssert.Contains(html, "Only the current page is rendered.");
            StringAssert.Contains(html, "Also search complete A/B values (slower)");
            StringAssert.Contains(html, "loadEnhancedDiff();");
            StringAssert.Contains(html, "https://cdn.jsdelivr.net/npm/diff@9.0.0/dist/diff.min.js");
            StringAssert.Contains(html, "https://cdn.jsdelivr.net/npm/diff2html@3.4.56/bundles/js/diff2html.min.js");
            StringAssert.Contains(html, "https://cdn.jsdelivr.net/npm/diff2html@3.4.56/bundles/css/diff2html.min.css");
            StringAssert.Contains(html, "createTwoFilesPatch");
            StringAssert.Contains(html, "outputFormat:elements.diffFormat.value");
            StringAssert.Contains(html, "class='ec-column-resizer'");
            StringAssert.Contains(html, "data-column-index='9'");
            StringAssert.Contains(html, "setPointerCapture(event.pointerId)");
            StringAssert.Contains(html, "style.minWidth = `${tableWidth}px`");
            StringAssert.Contains(html, "function fitResultsTableToContainer()");
            StringAssert.Contains(html, "initializeColumnResizing();");
            StringAssert.Contains(html, "body { margin:0; overflow:hidden;");
            StringAssert.Contains(html, ".shell { width:100%; height:100%; margin:0; padding:20px clamp(14px,1.25vw,32px);");
            Assert.IsFalse(html.Contains("max-width:1800px"));
            StringAssert.Contains(html, ".table-wrap { min-height:0; flex:1 1 auto; overflow:auto;");
            StringAssert.Contains(html, "scrollbar-gutter:stable both-edges");
            StringAssert.Contains(html, ".filters { flex:0 0 auto; display:grid;");
            StringAssert.Contains(html, "repeat(6,minmax(130px,1fr))");
            StringAssert.Contains(html, "align-items:start;");
            StringAssert.Contains(html, ".filters > .button { margin-top:20px; }");
            StringAssert.Contains(html, "#resultsTable > tbody td { padding:9px;");
            Assert.IsFalse(html.Contains("\n    table { width:1765px;"));
            Assert.IsFalse(html.Contains("\n    td { padding:9px;"));
            StringAssert.Contains(html, "class='action-column'");
            StringAssert.Contains(html, "id='classificationFilter'");
            StringAssert.Contains(html, "All classifications");
            StringAssert.Contains(html, "addOptions(elements.classificationFilter, classifications, 'No classification')");
            StringAssert.Contains(html, "filters.classification === '__none__' ? !row.classification : row.classification === filters.classification");
            StringAssert.Contains(html, "'classificationFilter'");
            StringAssert.Contains(html, "toggleEnhancedDiff");
            StringAssert.Contains(html, "Show normal values");
            StringAssert.Contains(html, "class='drawer-actions'><button id='showDiff'");
            StringAssert.Contains(html, "class='ec-diff-toolbar' role='toolbar'");
            StringAssert.Contains(html, "display:flex !important");
            StringAssert.Contains(html, "id='valuePaneResizer' class='ec-pane-resizer'");
            StringAssert.Contains(html, "function initializeValuePaneResizing()");
            StringAssert.Contains(html, "valuePaneRatio = Math.max(0.15, Math.min(0.85");
            StringAssert.Contains(html, "window.requestAnimationFrame(applyValuePaneSplit)");
            StringAssert.Contains(html, ".diff-panel { min-width:0; min-height:0; flex:1 1 auto; overflow:scroll;");
            StringAssert.Contains(html, "#diffOutput .d2h-file-side-diff { overflow:visible; }");
            StringAssert.Contains(html, "#diffOutput .d2h-code-line-ctn { display:block; white-space:pre-wrap;");
            StringAssert.Contains(html, "normalized.replace(/>\\s*</g, '>\\n<')");
            Assert.IsFalse(html.Contains("third-party code"));
            StringAssert.Contains(html, new string('x', 40000));
            StringAssert.Contains(html, "\"rowCount\":2");
            // CDN files are loaded dynamically so the report retains a working offline fallback.
            Assert.IsFalse(html.Contains("<script src="));
            Assert.IsFalse(html.Contains("<link rel='stylesheet'"));
        }

        [TestMethod]
        public void MarksOnlyChangedRowsAsInspectable()
        {
            var changed = Issue(1, DifferenceSeverity.Critical, ComparisonScope.Report, "RDL", "<Report A='1' />", "<Report A='2' />");
            var missing = new ComparisonIssue(
                DifferenceSeverity.High,
                ComparisonScope.Report,
                DifferenceKind.MissingInEnvironmentB,
                string.Empty,
                string.Empty,
                "report-2",
                "Missing report",
                "Report presence",
                "Present",
                "Missing",
                "The report is missing from Environment B.");

            string html;
            using (var writer = new StringWriter())
            {
                new HtmlExportService().Write(writer, new[] { changed, missing });
                html = writer.ToString();
            }

            StringAssert.Contains(html, "\"difference\":\"Changed\",\"inspectable\":true");
            StringAssert.Contains(html, "\"difference\":\"Missing in Environment B\",\"inspectable\":false");
            StringAssert.Contains(html, "if (item.inspectable)");
            StringAssert.Contains(html, "Presence-only differences do not have two component definitions to inspect.");
        }

        [TestMethod]
        public void IncludesCompleteNormalizedAndRawReportRdlForReportInspection()
        {
            const string reportKey = "report-1";
            const string normalizedA = "<Report><DataSets><DataSet Name='A' /></DataSets></Report>";
            const string normalizedB = "<Report><DataSets><DataSet Name='B' /></DataSets></Report>";
            const string rawA = "<Report>\r\n  <DataSets />\r\n</Report>";
            const string rawB = "<Report>\n<DataSources />\n</Report>";
            var reportA = new ReportMetadataInfo(reportKey, "Student report", new Dictionary<string, string>
            {
                ["RDL"] = normalizedA,
                ["Raw RDL"] = rawA
            });
            var reportB = new ReportMetadataInfo(reportKey, "Student report", new Dictionary<string, string>
            {
                ["RDL"] = normalizedB,
                ["Raw RDL"] = rawB
            });
            var issue = new ComparisonIssue(
                DifferenceSeverity.Critical,
                ComparisonScope.Report,
                DifferenceKind.Changed,
                string.Empty,
                string.Empty,
                reportKey,
                "Student report",
                "Description",
                "A",
                "B",
                "The report differs.");
            var result = new MetadataComparisonResult(
                new EnvironmentMetadataSnapshot(Array.Empty<TableMetadataInfo>(), includedAreas: ComparisonAreas.Reports, reports: new[] { reportA }),
                new EnvironmentMetadataSnapshot(Array.Empty<TableMetadataInfo>(), includedAreas: ComparisonAreas.Reports, reports: new[] { reportB }),
                new[] { issue });

            string html;
            using (var writer = new StringWriter())
            {
                new HtmlExportService().Write(writer, result);
                html = writer.ToString();
            }

            StringAssert.Contains(html, "\"reportDefinitions\":[{");
            StringAssert.Contains(html, "\"key\":\"report-1\"");
            StringAssert.Contains(html, "\"aRdl\":\"\\u003CReport\\u003E\\u003CDataSets\\u003E");
            StringAssert.Contains(html, "\"bRawRdl\":\"\\u003CReport\\u003E\\n\\u003CDataSources /\\u003E");
            StringAssert.Contains(html, "Report RDL XML (normalized)");
            StringAssert.Contains(html, "Report RDL XML (raw retrieval)");
        }

        [TestMethod]
        public void UsesSeparateEnvironmentKeysForFallbackMatchedReportInspection()
        {
            const string reportKeyA = "report|id:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            const string reportKeyB = "report|id:bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
            var reportA = new ReportMetadataInfo(reportKeyA, "Account Summary", new Dictionary<string, string>
            {
                ["RDL"] = "<Report Environment='A' />",
                ["Raw RDL"] = "<Report Raw='A' />"
            });
            var reportB = new ReportMetadataInfo(reportKeyB, "Account Summary", new Dictionary<string, string>
            {
                ["RDL"] = "<Report Environment='B' />",
                ["Raw RDL"] = "<Report Raw='B' />"
            });
            var issue = new ComparisonIssue(
                DifferenceSeverity.High,
                ComparisonScope.Report,
                DifferenceKind.Changed,
                string.Empty,
                string.Empty,
                reportKeyA,
                "Account Summary",
                "Report ID",
                "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                "Fallback matched.",
                environmentAComponentKey: reportKeyA,
                environmentBComponentKey: reportKeyB);
            var result = new MetadataComparisonResult(
                new EnvironmentMetadataSnapshot(Array.Empty<TableMetadataInfo>(), includedAreas: ComparisonAreas.Reports, reports: new[] { reportA }),
                new EnvironmentMetadataSnapshot(Array.Empty<TableMetadataInfo>(), includedAreas: ComparisonAreas.Reports, reports: new[] { reportB }),
                new[] { issue });

            string html;
            using (var writer = new StringWriter())
            {
                new HtmlExportService().Write(writer, result);
                html = writer.ToString();
            }

            StringAssert.Contains(html, "\"key\":\"report|id:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"");
            StringAssert.Contains(html, "\"aRawRdl\":\"\\u003CReport Raw='A' /\\u003E\"");
            StringAssert.Contains(html, "\"bRawRdl\":\"\\u003CReport Raw='B' /\\u003E\"");
        }

        [TestMethod]
        public void EncodesMetadataSoItCannotCloseTheJsonScriptElement()
        {
            const string hostile = "</script><script>alert(\"owned\")</script>&";
            string html;
            using (var writer = new StringWriter())
            {
                new HtmlExportService().Write(
                    writer,
                    new[] { Issue(1, DifferenceSeverity.Critical, ComparisonScope.Form, "Form XML", hostile, string.Empty) });
                html = writer.ToString();
            }

            Assert.IsFalse(html.Contains(hostile));
            StringAssert.Contains(html, "\\u003C/script\\u003E\\u003Cscript\\u003Ealert(\\\"owned\\\")\\u003C/script\\u003E\\u0026");
            Assert.AreEqual(2, CountOccurrences(html, "</script>"));
        }

        [TestMethod]
        public void StreamsLargeRowSetsWithoutCreatingTableRowsInTheDocument()
        {
            var issues = Enumerable.Range(1, 5000)
                .Select(index => Issue(
                    index,
                    index % 2 == 0 ? DifferenceSeverity.High : DifferenceSeverity.Medium,
                    ComparisonScope.Column,
                    "Display name",
                    "Environment A " + index,
                    "Environment B " + index))
                .ToList();

            string html;
            int count;
            using (var writer = new StringWriter())
            {
                count = new HtmlExportService().Write(writer, issues);
                html = writer.ToString();
            }

            Assert.AreEqual(5000, count);
            StringAssert.Contains(html, "\"rowCount\":5000");
            Assert.AreEqual(0, CountOccurrences(html, "<tbody id='resultsBody'><tr>"));
            StringAssert.Contains(html, "filteredRows.slice(start, start + pageSize)");
        }

        private static ComparisonIssue Issue(
            int index,
            DifferenceSeverity severity,
            ComparisonScope scope,
            string property,
            string valueA,
            string valueB)
        {
            return new ComparisonIssue(
                severity,
                scope,
                DifferenceKind.Changed,
                "ata_student",
                "Student",
                "component-" + index,
                "Component " + index,
                property,
                valueA,
                valueB,
                "The metadata differs.",
                "Standard",
                "Preview A " + index,
                "Preview B " + index);
        }

        private static int CountOccurrences(string value, string search)
        {
            var count = 0;
            var offset = 0;
            while ((offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += search.Length;
            }

            return count;
        }
    }
}
