using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnvironmentComparison.Domain;

namespace EnvironmentComparison.Services
{
    public sealed class RawMetadataExportService
    {
        private static readonly string[] LeadingHeaders =
        {
            "Environment",
            "Scope",
            "Table logical name",
            "Table display name",
            "Table classification",
            "Custom table",
            "Custom component",
            "Component key",
            "Component name",
            "Property"
        };

        public int Write(TextWriter writer, MetadataComparisonResult result)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (result == null) throw new ArgumentNullException(nameof(result));

            var valuePartCount = Rows(result)
                .Select(row => CsvExportService.SplitForExcel(row.Value).Count)
                .DefaultIfEmpty(1)
                .Max();
            writer.WriteLine(string.Join(",", Headers(valuePartCount).Select(CsvExportService.Escape)));

            var count = 0;
            foreach (var row in Rows(result))
            {
                var leadingValues = new[]
                {
                    row.Environment,
                    row.Scope,
                    row.TableLogicalName,
                    row.TableDisplayName,
                    row.TableClassification,
                    row.CustomTable,
                    row.CustomComponent,
                    row.ComponentKey,
                    row.ComponentName,
                    row.Property
                };
                var values = leadingValues.Select(CsvExportService.Escape)
                    .Concat(ValueParts(row.Value, valuePartCount));
                writer.WriteLine(string.Join(",", values));
                count++;
            }

            return count;
        }

        private static IEnumerable<RawMetadataRow> Rows(MetadataComparisonResult result)
        {
            return Rows("Environment A", result.EnvironmentA)
                .Concat(Rows("Environment B", result.EnvironmentB));
        }

        private static IEnumerable<RawMetadataRow> Rows(string environment, EnvironmentMetadataSnapshot snapshot)
        {
            var tables = snapshot.Tables.ToDictionary(table => table.LogicalName, StringComparer.OrdinalIgnoreCase);
            if ((snapshot.IncludedAreas & ComparisonAreas.TableMetadata) != 0)
            {
                foreach (var table in snapshot.Tables)
                {
                    foreach (var property in table.Properties.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        yield return Row(environment, "Table", table, table.LogicalName, table.DisplayName, table.CustomTable, property);
                    }
                }
            }

            if ((snapshot.IncludedAreas & ComparisonAreas.Columns) != 0)
            {
                foreach (var table in snapshot.Tables)
                {
                    foreach (var column in table.Columns)
                    {
                        foreach (var property in column.Properties.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                        {
                            yield return Row(environment, "Column", table, column.LogicalName, column.DisplayName, column.CustomComponent, property);
                        }
                    }
                }
            }

            if ((snapshot.IncludedAreas & ComparisonAreas.Forms) != 0)
            {
                foreach (var form in snapshot.Forms)
                {
                    tables.TryGetValue(form.TableLogicalName, out var table);
                    foreach (var property in form.Properties.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        yield return Row(environment, "Form", table, form.TableLogicalName, form.Key, form.Name, "Unknown", property);
                    }
                }
            }

            if ((snapshot.IncludedAreas & ComparisonAreas.Views) != 0)
            {
                foreach (var view in snapshot.Views)
                {
                    tables.TryGetValue(view.TableLogicalName, out var table);
                    foreach (var property in view.Properties.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        yield return Row(environment, "View", table, view.TableLogicalName, view.Key, view.Name, "Unknown", property);
                    }
                }
            }
        }

        private static RawMetadataRow Row(
            string environment,
            string scope,
            TableMetadataInfo table,
            string componentKey,
            string componentName,
            string customComponent,
            KeyValuePair<string, string> property)
        {
            return Row(environment, scope, table, table.LogicalName, componentKey, componentName, customComponent, property);
        }

        private static RawMetadataRow Row(
            string environment,
            string scope,
            TableMetadataInfo? table,
            string tableLogicalName,
            string componentKey,
            string componentName,
            string customComponent,
            KeyValuePair<string, string> property)
        {
            return new RawMetadataRow(
                environment,
                scope,
                tableLogicalName,
                table?.DisplayName ?? string.Empty,
                table?.Classification ?? string.Empty,
                table?.CustomTable ?? "Unknown",
                customComponent,
                componentKey,
                componentName,
                property.Key,
                property.Value);
        }

        private static IEnumerable<string> Headers(int valuePartCount)
        {
            foreach (var header in LeadingHeaders)
            {
                yield return header;
            }

            if (valuePartCount == 1)
            {
                yield return "Value";
                yield break;
            }

            for (var part = 1; part <= valuePartCount; part++)
            {
                yield return $"Value (part {part} of {valuePartCount})";
            }
        }

        private static IEnumerable<string> ValueParts(string value, int valuePartCount)
        {
            var parts = CsvExportService.SplitForExcel(value);
            for (var index = 0; index < valuePartCount; index++)
            {
                var part = index < parts.Count ? parts[index] : string.Empty;
                yield return CsvExportService.Escape(part, index == 0);
            }
        }

        private sealed class RawMetadataRow
        {
            public RawMetadataRow(
                string environment,
                string scope,
                string tableLogicalName,
                string tableDisplayName,
                string tableClassification,
                string customTable,
                string customComponent,
                string componentKey,
                string componentName,
                string property,
                string value)
            {
                Environment = environment;
                Scope = scope;
                TableLogicalName = tableLogicalName;
                TableDisplayName = tableDisplayName;
                TableClassification = tableClassification;
                CustomTable = customTable;
                CustomComponent = customComponent;
                ComponentKey = componentKey;
                ComponentName = componentName;
                Property = property;
                Value = value;
            }

            public string Environment { get; }
            public string Scope { get; }
            public string TableLogicalName { get; }
            public string TableDisplayName { get; }
            public string TableClassification { get; }
            public string CustomTable { get; }
            public string CustomComponent { get; }
            public string ComponentKey { get; }
            public string ComponentName { get; }
            public string Property { get; }
            public string Value { get; }
        }
    }
}
