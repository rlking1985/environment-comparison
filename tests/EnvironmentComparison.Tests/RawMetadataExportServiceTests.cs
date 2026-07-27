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
                    "Table classification", "Standard",
                    "Custom table", "Yes"),
                new[]
                {
                    new ColumnMetadataInfo(
                        "new_name",
                        Properties("Display name", "Name", "Custom component", "Yes"))
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

            string csv;
            int rowCount;
            using (var writer = new StringWriter())
            {
                rowCount = new RawMetadataExportService().Write(writer, result);
                csv = writer.ToString();
            }

            Assert.AreEqual(0, result.Issues.Count);
            Assert.IsTrue(rowCount > 0);
            StringAssert.Contains(csv, "\"Environment\",\"Scope\"");
            StringAssert.Contains(csv, "\"Environment A\"");
            StringAssert.Contains(csv, "\"Environment B\"");
            StringAssert.Contains(csv, "\"Custom table\",\"Custom component\"");
            StringAssert.Contains(csv, "\"Form ID unique\"");
            StringAssert.Contains(csv, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            StringAssert.Contains(csv, "<form>\r\n  <tab id=\"\"main\"\" />");
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
