using System;
using System.Linq;
using EnvironmentComparison.Domain;
using EnvironmentComparison.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EnvironmentComparison.Tests
{
    [TestClass]
    public sealed class CsvExportServiceTests
    {
        [TestMethod]
        public void CsvContainsAllIssueFieldsAndEscapesValues()
        {
            var issue = new ComparisonIssue(
                DifferenceSeverity.Critical,
                ComparisonScope.Column,
                DifferenceKind.Changed,
                "new_student",
                "Student",
                "new_code",
                "Student \"code\"",
                "Maximum length",
                "100, legacy",
                "50",
                "Line one\r\nLine two",
                "Intersect");

            var csv = new CsvExportService().Create(new[] { issue });

            StringAssert.Contains(csv, "\"new_student\"");
            StringAssert.Contains(csv, "\"Student \"\"code\"\"\"");
            StringAssert.Contains(csv, "\"Table classification\"");
            StringAssert.Contains(csv, "\"Intersect\"");
            StringAssert.Contains(csv, "\"100, legacy\"");
            StringAssert.Contains(csv, "\"Line one\r\nLine two\"");
        }

        [TestMethod]
        public void CsvExportsTheFullDefinitionRatherThanThePreviewFingerprint()
        {
            const string definitionA = "<form><tab id=\"a\" /></form>";
            const string definitionB = "<form><tab id=\"b\" /></form>";
            var issue = new ComparisonIssue(
                DifferenceSeverity.Critical,
                ComparisonScope.Form,
                DifferenceKind.Changed,
                "new_student",
                "Student",
                "new_student|unique:main",
                "Main form",
                "Form XML",
                definitionA,
                definitionB,
                "The form definition is different.",
                "Standard",
                "SHA-256 preview-a",
                "SHA-256 preview-b");

            var csv = new CsvExportService().Create(new[] { issue });

            StringAssert.Contains(csv, "<form><tab id=\"\"a\"\" /></form>");
            StringAssert.Contains(csv, "<form><tab id=\"\"b\"\" /></form>");
            Assert.IsFalse(csv.Contains("SHA-256 preview-a"));
            Assert.IsFalse(csv.Contains("SHA-256 preview-b"));
        }

        [TestMethod]
        public void CsvSplitsOversizedValuesIntoExcelSafeColumnsWithoutDataLoss()
        {
            var valueA = new string('A', 70000);
            var valueB = new string('B', 33000);
            var issue = new ComparisonIssue(
                DifferenceSeverity.Critical,
                ComparisonScope.Column,
                DifferenceKind.Changed,
                "duplicaterule",
                "Duplicate Detection Rule",
                "baseentitytypecode",
                "Base Record Type",
                "Choice values",
                valueA,
                valueB,
                "The choice values are different.",
                "Standard");

            var csv = new CsvExportService().Create(new[] { issue });
            var lines = csv.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var headers = lines[0].Split(',').Select(Unquote).ToArray();
            var values = lines[1].Split(',').Select(Unquote).ToArray();

            Assert.AreEqual(16, headers.Length);
            Assert.AreEqual(headers.Length, values.Length);
            Assert.AreEqual("Environment A (part 1 of 3)", headers[9]);
            Assert.AreEqual("Environment A (part 3 of 3)", headers[11]);
            Assert.AreEqual("Environment B (part 1 of 3)", headers[12]);
            Assert.AreEqual("Environment B (part 3 of 3)", headers[14]);
            Assert.IsTrue(values.Skip(9).Take(6).All(value => value.Length <= CsvExportService.ExcelSafeCellLength));
            Assert.AreEqual(valueA, string.Concat(values.Skip(9).Take(3)));
            Assert.AreEqual(valueB, string.Concat(values.Skip(12).Take(3)));
        }

        [TestMethod]
        public void CsvNeutralizesSpreadsheetFormulas()
        {
            Assert.AreEqual("\"'=HYPERLINK(\"\"bad\"\")\"", CsvExportService.Escape("=HYPERLINK(\"bad\")"));
            Assert.AreEqual("\"'+1\"", CsvExportService.Escape("+1"));
            Assert.AreEqual("\"'-1\"", CsvExportService.Escape("-1"));
            Assert.AreEqual("\"'@name\"", CsvExportService.Escape("@name"));
        }

        private static string Unquote(string value)
        {
            return value.Length >= 2 && value[0] == '\"' && value[value.Length - 1] == '\"'
                ? value.Substring(1, value.Length - 2).Replace("\"\"", "\"")
                : value;
        }
    }
}
