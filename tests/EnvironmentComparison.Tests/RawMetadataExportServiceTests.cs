using System;
using System.Collections.Generic;
using System.IO;
using EnvironmentComparison.Domain;
using EnvironmentComparison.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EnvironmentComparison.Tests
{
    [TestClass]
    public sealed class RawMetadataExportServiceTests
    {
        [TestMethod]
        public void ExportsEveryLoadedPropertyEvenWhenThereAreNoDifferences()
        {
            var table = new TableMetadataInfo(
                "new_student",
                Properties(
                    "Display name", "Student",
                    "Table classification", "Standard"),
                new[]
                {
                    new ColumnMetadataInfo(
                        "new_name",
                        Properties("Display name", "Name"))
                });
            var form = new FormMetadataInfo(
                "new_student|id:12345678-1234-1234-1234-1234567890ab",
                "new_student",
                "Information",
                Properties(
                    "Form ID", "12345678-1234-1234-1234-1234567890ab",
                    "Form ID unique", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                    "Form XML", "<form><tab id=\"main\" /></form>",
                    "Raw Form XML", "\r\n<form>\r\n  <tab id=\"main\" />\r\n</form>\r\n"));
            var snapshot = new EnvironmentMetadataSnapshot(
                new[] { table },
                new[] { form },
                includedAreas: ComparisonAreas.TableMetadata | ComparisonAreas.Columns | ComparisonAreas.Forms);
            var result = new MetadataComparisonService().Compare(snapshot, snapshot);

            string json;
            int propertyCount;
            using (var writer = new StringWriter())
            {
                propertyCount = new RawMetadataExportService().Write(writer, result);
                json = writer.ToString();
            }

            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreEqual(14, propertyCount);
            StringAssert.StartsWith(json, "{");
            StringAssert.Contains(json, "\"exportType\": \"Dataverse environment raw metadata\"");
            StringAssert.Contains(json, "\"environment\": \"Environment A\"");
            StringAssert.Contains(json, "\"environment\": \"Environment B\"");
            StringAssert.Contains(json, "\"tables\": [");
            StringAssert.Contains(json, "\"forms\": [");
            StringAssert.Contains(json, "\"Form ID unique\": \"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"");
            StringAssert.Contains(json, "<form>\\r\\n  <tab id=\\\"main\\\" />");
            Assert.IsFalse(json.Contains("Value (part"));
        }

        [TestMethod]
        public void RecordsWhetherTheSnapshotIncludesUnpublishedMetadata()
        {
            var snapshot = new EnvironmentMetadataSnapshot(
                Array.Empty<TableMetadataInfo>(),
                includedAreas: ComparisonAreas.Forms,
                includesUnpublishedMetadata: true);
            var result = new MetadataComparisonService().Compare(snapshot, snapshot);

            string json;
            using (var writer = new StringWriter())
            {
                new RawMetadataExportService().Write(writer, result);
                json = writer.ToString();
            }

            StringAssert.Contains(json, "\"metadataMode\": \"PublishedAndUnpublished\"");
            StringAssert.Contains(json, "\"includedAreas\": [\"Forms\"]");
        }

        [TestMethod]
        public void ExportsReportDefinitionsAndPublicationMetadataAsStructuredJson()
        {
            var report = new ReportMetadataInfo(
                "report|id:12345678-aaaa-bbbb-cccc-1234567890ab",
                "Student summary",
                Properties(
                    "Associated tables", "account | contact",
                    "Raw RDL", "\r\n<Report xmlns=\"urn:report\">\r\n  <DataSets />\r\n</Report>\r\n"));
            var snapshot = new EnvironmentMetadataSnapshot(
                Array.Empty<TableMetadataInfo>(),
                includedAreas: ComparisonAreas.Reports,
                reports: new[] { report });
            var result = new MetadataComparisonService().Compare(snapshot, snapshot);

            string json;
            int propertyCount;
            using (var writer = new StringWriter())
            {
                propertyCount = new RawMetadataExportService().Write(writer, result);
                json = writer.ToString();
            }

            Assert.AreEqual(4, propertyCount);
            StringAssert.Contains(json, "\"includedAreas\": [\"Reports\"]");
            StringAssert.Contains(json, "\"reports\": [");
            StringAssert.Contains(json, "\"reports\": 1");
            StringAssert.Contains(json, "\"Associated tables\": \"account | contact\"");
            StringAssert.Contains(json, "<Report xmlns=\\\"urn:report\\\">");
        }

        private static Dictionary<string, string> Properties(params string[] values)
        {
            var properties = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < values.Length; index += 2)
            {
                properties[values[index]] = values[index + 1];
            }

            return properties;
        }
    }
}
