using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using EnvironmentComparison.Domain;

namespace EnvironmentComparison.Services
{
    public sealed class RawMetadataExportService
    {
        public int Write(TextWriter writer, MetadataComparisonResult result)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (result == null) throw new ArgumentNullException(nameof(result));

            var propertyCount = 0;
            writer.WriteLine("{");
            WriteNamedString(writer, 1, "exportType", "Dataverse environment raw metadata", true);
            WriteNamedNumber(writer, 1, "formatVersion", 3, true);
            WriteNamedString(
                writer,
                1,
                "description",
                "Structured snapshots used by the comparison. Raw source XML and normalized comparison values are both retained in component properties.",
                true);
            Indent(writer, 1);
            writer.WriteLine("\"environments\": [");
            WriteSnapshot(writer, "Environment A", result.EnvironmentA, 2, ref propertyCount);
            writer.WriteLine(",");
            WriteSnapshot(writer, "Environment B", result.EnvironmentB, 2, ref propertyCount);
            writer.WriteLine();
            Indent(writer, 1);
            writer.WriteLine("]");
            writer.WriteLine("}");
            return propertyCount;
        }

        private static void WriteSnapshot(
            TextWriter writer,
            string environment,
            EnvironmentMetadataSnapshot snapshot,
            int indent,
            ref int propertyCount)
        {
            Indent(writer, indent);
            writer.WriteLine("{");
            WriteNamedString(writer, indent + 1, "environment", environment, true);
            WriteNamedString(
                writer,
                indent + 1,
                "metadataMode",
                snapshot.IncludesUnpublishedMetadata ? "PublishedAndUnpublished" : "Published",
                true);
            WriteAreas(writer, snapshot.IncludedAreas, indent + 1);
            writer.WriteLine(",");
            WriteCounts(writer, snapshot, indent + 1);
            writer.WriteLine(",");
            WriteTables(writer, snapshot.Tables, indent + 1, ref propertyCount);
            writer.WriteLine(",");
            WriteForms(writer, snapshot.Forms, indent + 1, ref propertyCount);
            writer.WriteLine(",");
            WriteViews(writer, snapshot.Views, indent + 1, ref propertyCount);
            writer.WriteLine(",");
            WriteReports(writer, snapshot.Reports, indent + 1, ref propertyCount);
            writer.WriteLine();
            Indent(writer, indent);
            writer.Write("}");
        }

        private static void WriteAreas(TextWriter writer, ComparisonAreas areas, int indent)
        {
            var selected = new[]
            {
                new { Area = ComparisonAreas.TableMetadata, Name = "TableMetadata" },
                new { Area = ComparisonAreas.Columns, Name = "Columns" },
                new { Area = ComparisonAreas.Forms, Name = "Forms" },
                new { Area = ComparisonAreas.Views, Name = "Views" },
                new { Area = ComparisonAreas.Reports, Name = "Reports" }
            }.Where(item => (areas & item.Area) != 0).Select(item => item.Name).ToList();

            Indent(writer, indent);
            writer.Write("\"includedAreas\": [");
            for (var index = 0; index < selected.Count; index++)
            {
                if (index > 0) writer.Write(", ");
                WriteString(writer, selected[index]);
            }

            writer.Write("]");
        }

        private static void WriteCounts(TextWriter writer, EnvironmentMetadataSnapshot snapshot, int indent)
        {
            Indent(writer, indent);
            writer.WriteLine("\"counts\": {");
            WriteNamedNumber(writer, indent + 1, "tables", snapshot.Tables.Count, true);
            WriteNamedNumber(writer, indent + 1, "columns", snapshot.ColumnCount, true);
            WriteNamedNumber(writer, indent + 1, "forms", snapshot.Forms.Count, true);
            WriteNamedNumber(writer, indent + 1, "views", snapshot.Views.Count, true);
            WriteNamedNumber(writer, indent + 1, "reports", snapshot.Reports.Count, false);
            Indent(writer, indent);
            writer.Write("}");
        }

        private static void WriteTables(
            TextWriter writer,
            IReadOnlyList<TableMetadataInfo> tables,
            int indent,
            ref int propertyCount)
        {
            Indent(writer, indent);
            writer.WriteLine("\"tables\": [");
            for (var tableIndex = 0; tableIndex < tables.Count; tableIndex++)
            {
                var table = tables[tableIndex];
                Indent(writer, indent + 1);
                writer.WriteLine("{");
                WriteNamedString(writer, indent + 2, "logicalName", table.LogicalName, true);
                WriteNamedString(writer, indent + 2, "displayName", table.DisplayName, true);
                WriteNamedString(writer, indent + 2, "classification", table.Classification, true);
                WriteProperties(writer, table.Properties, indent + 2, "properties", ref propertyCount);
                writer.WriteLine(",");
                WriteColumns(writer, table.Columns, indent + 2, ref propertyCount);
                writer.WriteLine();
                Indent(writer, indent + 1);
                writer.Write("}");
                if (tableIndex < tables.Count - 1) writer.Write(",");
                writer.WriteLine();
            }

            Indent(writer, indent);
            writer.Write("]");
        }

        private static void WriteColumns(
            TextWriter writer,
            IReadOnlyList<ColumnMetadataInfo> columns,
            int indent,
            ref int propertyCount)
        {
            Indent(writer, indent);
            writer.WriteLine("\"columns\": [");
            for (var index = 0; index < columns.Count; index++)
            {
                var column = columns[index];
                Indent(writer, indent + 1);
                writer.WriteLine("{");
                WriteNamedString(writer, indent + 2, "logicalName", column.LogicalName, true);
                WriteNamedString(writer, indent + 2, "displayName", column.DisplayName, true);
                WriteProperties(writer, column.Properties, indent + 2, "properties", ref propertyCount);
                writer.WriteLine();
                Indent(writer, indent + 1);
                writer.Write("}");
                if (index < columns.Count - 1) writer.Write(",");
                writer.WriteLine();
            }

            Indent(writer, indent);
            writer.Write("]");
        }

        private static void WriteForms(
            TextWriter writer,
            IReadOnlyList<FormMetadataInfo> forms,
            int indent,
            ref int propertyCount)
        {
            Indent(writer, indent);
            writer.WriteLine("\"forms\": [");
            for (var index = 0; index < forms.Count; index++)
            {
                var form = forms[index];
                Indent(writer, indent + 1);
                writer.WriteLine("{");
                WriteNamedString(writer, indent + 2, "key", form.Key, true);
                WriteNamedString(writer, indent + 2, "tableLogicalName", form.TableLogicalName, true);
                WriteNamedString(writer, indent + 2, "name", form.Name, true);
                WriteProperties(writer, form.Properties, indent + 2, "properties", ref propertyCount);
                writer.WriteLine();
                Indent(writer, indent + 1);
                writer.Write("}");
                if (index < forms.Count - 1) writer.Write(",");
                writer.WriteLine();
            }

            Indent(writer, indent);
            writer.Write("]");
        }

        private static void WriteViews(
            TextWriter writer,
            IReadOnlyList<ViewMetadataInfo> views,
            int indent,
            ref int propertyCount)
        {
            Indent(writer, indent);
            writer.WriteLine("\"views\": [");
            for (var index = 0; index < views.Count; index++)
            {
                var view = views[index];
                Indent(writer, indent + 1);
                writer.WriteLine("{");
                WriteNamedString(writer, indent + 2, "key", view.Key, true);
                WriteNamedString(writer, indent + 2, "tableLogicalName", view.TableLogicalName, true);
                WriteNamedString(writer, indent + 2, "name", view.Name, true);
                WriteProperties(writer, view.Properties, indent + 2, "properties", ref propertyCount);
                writer.WriteLine();
                Indent(writer, indent + 1);
                writer.Write("}");
                if (index < views.Count - 1) writer.Write(",");
                writer.WriteLine();
            }

            Indent(writer, indent);
            writer.Write("]");
        }

        private static void WriteReports(
            TextWriter writer,
            IReadOnlyList<ReportMetadataInfo> reports,
            int indent,
            ref int propertyCount)
        {
            Indent(writer, indent);
            writer.WriteLine("\"reports\": [");
            for (var index = 0; index < reports.Count; index++)
            {
                var report = reports[index];
                Indent(writer, indent + 1);
                writer.WriteLine("{");
                WriteNamedString(writer, indent + 2, "key", report.Key, true);
                WriteNamedString(writer, indent + 2, "name", report.Name, true);
                WriteProperties(writer, report.Properties, indent + 2, "properties", ref propertyCount);
                writer.WriteLine();
                Indent(writer, indent + 1);
                writer.Write("}");
                if (index < reports.Count - 1) writer.Write(",");
                writer.WriteLine();
            }

            Indent(writer, indent);
            writer.Write("]");
        }

        private static void WriteProperties(
            TextWriter writer,
            IReadOnlyDictionary<string, string> properties,
            int indent,
            string propertyName,
            ref int propertyCount)
        {
            var ordered = properties.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase).ToList();
            Indent(writer, indent);
            WriteString(writer, propertyName);
            writer.WriteLine(": {");
            for (var index = 0; index < ordered.Count; index++)
            {
                var property = ordered[index];
                Indent(writer, indent + 1);
                WriteString(writer, property.Key);
                writer.Write(": ");
                WriteString(writer, property.Value);
                if (index < ordered.Count - 1) writer.Write(",");
                writer.WriteLine();
                propertyCount++;
            }

            Indent(writer, indent);
            writer.Write("}");
        }

        private static void WriteNamedString(
            TextWriter writer,
            int indent,
            string name,
            string value,
            bool trailingComma)
        {
            Indent(writer, indent);
            WriteString(writer, name);
            writer.Write(": ");
            WriteString(writer, value);
            if (trailingComma) writer.Write(",");
            writer.WriteLine();
        }

        private static void WriteNamedNumber(
            TextWriter writer,
            int indent,
            string name,
            int value,
            bool trailingComma)
        {
            Indent(writer, indent);
            WriteString(writer, name);
            writer.Write(": ");
            writer.Write(value.ToString(CultureInfo.InvariantCulture));
            if (trailingComma) writer.Write(",");
            writer.WriteLine();
        }

        private static void WriteString(TextWriter writer, string value)
        {
            writer.Write('"');
            foreach (var character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '"': writer.Write("\\\""); break;
                    case '\\': writer.Write("\\\\"); break;
                    case '\b': writer.Write("\\b"); break;
                    case '\f': writer.Write("\\f"); break;
                    case '\n': writer.Write("\\n"); break;
                    case '\r': writer.Write("\\r"); break;
                    case '\t': writer.Write("\\t"); break;
                    default:
                        if (character < 0x20)
                        {
                            writer.Write("\\u");
                            writer.Write(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            writer.Write(character);
                        }

                        break;
                }
            }

            writer.Write('"');
        }

        private static void Indent(TextWriter writer, int level)
        {
            writer.Write(new string(' ', level * 2));
        }
    }
}
