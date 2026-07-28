using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EnvironmentComparison.Domain
{
    [Flags]
    public enum ComparisonAreas
    {
        None = 0,
        TableMetadata = 1,
        Columns = 2,
        Forms = 4,
        Views = 8,
        Reports = 16,
        All = TableMetadata | Columns | Forms | Views | Reports
    }

    public sealed class EnvironmentMetadataSnapshot
    {
        public EnvironmentMetadataSnapshot(
            IEnumerable<TableMetadataInfo> tables,
            IEnumerable<FormMetadataInfo>? forms = null,
            IEnumerable<ViewMetadataInfo>? views = null,
            ComparisonAreas includedAreas = ComparisonAreas.TableMetadata | ComparisonAreas.Columns,
            bool includesUnpublishedMetadata = false,
            IEnumerable<ReportMetadataInfo>? reports = null)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            Tables = new ReadOnlyCollection<TableMetadataInfo>(tables.OrderBy(table => table.LogicalName, StringComparer.OrdinalIgnoreCase).ToList());
            Forms = new ReadOnlyCollection<FormMetadataInfo>(
                (forms ?? Enumerable.Empty<FormMetadataInfo>()).OrderBy(form => form.Key, StringComparer.OrdinalIgnoreCase).ToList());
            Views = new ReadOnlyCollection<ViewMetadataInfo>(
                (views ?? Enumerable.Empty<ViewMetadataInfo>()).OrderBy(view => view.Key, StringComparer.OrdinalIgnoreCase).ToList());
            Reports = new ReadOnlyCollection<ReportMetadataInfo>(
                (reports ?? Enumerable.Empty<ReportMetadataInfo>()).OrderBy(report => report.Key, StringComparer.OrdinalIgnoreCase).ToList());
            IncludedAreas = includedAreas;
            IncludesUnpublishedMetadata = includesUnpublishedMetadata;
        }

        public IReadOnlyList<TableMetadataInfo> Tables { get; }

        public IReadOnlyList<FormMetadataInfo> Forms { get; }

        public IReadOnlyList<ViewMetadataInfo> Views { get; }

        public IReadOnlyList<ReportMetadataInfo> Reports { get; }

        public ComparisonAreas IncludedAreas { get; }

        public bool IncludesUnpublishedMetadata { get; }

        public int ColumnCount => Tables.Sum(table => table.Columns.Count);
    }

    public sealed class FormMetadataInfo
    {
        public FormMetadataInfo(string key, string tableLogicalName, string name, IDictionary<string, string> properties)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            TableLogicalName = tableLogicalName ?? string.Empty;
            Name = name ?? string.Empty;
            Properties = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(properties ?? throw new ArgumentNullException(nameof(properties)), StringComparer.Ordinal));
        }

        public string Key { get; }

        public string TableLogicalName { get; }

        public string Name { get; }

        public IReadOnlyDictionary<string, string> Properties { get; }

        public string GetProperty(string name) => Properties.TryGetValue(name, out var value) ? value : string.Empty;
    }

    public sealed class ViewMetadataInfo
    {
        public ViewMetadataInfo(string key, string tableLogicalName, string name, IDictionary<string, string> properties)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            TableLogicalName = tableLogicalName ?? string.Empty;
            Name = name ?? string.Empty;
            Properties = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(properties ?? throw new ArgumentNullException(nameof(properties)), StringComparer.Ordinal));
        }

        public string Key { get; }

        public string TableLogicalName { get; }

        public string Name { get; }

        public IReadOnlyDictionary<string, string> Properties { get; }

        public string GetProperty(string name) => Properties.TryGetValue(name, out var value) ? value : string.Empty;
    }

    public sealed class ReportMetadataInfo
    {
        public ReportMetadataInfo(string key, string name, IDictionary<string, string> properties)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Name = name ?? string.Empty;
            Properties = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(properties ?? throw new ArgumentNullException(nameof(properties)), StringComparer.Ordinal));
        }

        public string Key { get; }

        public string Name { get; }

        public IReadOnlyDictionary<string, string> Properties { get; }

        public string GetProperty(string name) => Properties.TryGetValue(name, out var value) ? value : string.Empty;
    }

    public sealed class TableMetadataInfo
    {
        public TableMetadataInfo(
            string logicalName,
            IDictionary<string, string> properties,
            IEnumerable<ColumnMetadataInfo> columns)
        {
            LogicalName = logicalName ?? throw new ArgumentNullException(nameof(logicalName));
            Properties = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(properties ?? throw new ArgumentNullException(nameof(properties)), StringComparer.Ordinal));
            Columns = new ReadOnlyCollection<ColumnMetadataInfo>(
                (columns ?? throw new ArgumentNullException(nameof(columns)))
                    .OrderBy(column => column.LogicalName, StringComparer.OrdinalIgnoreCase)
                    .ToList());
        }

        public string LogicalName { get; }

        public IReadOnlyDictionary<string, string> Properties { get; }

        public IReadOnlyList<ColumnMetadataInfo> Columns { get; }

        public string DisplayName => GetProperty("Display name");

        public string Classification => GetProperty("Table classification");

        public string GetProperty(string name) => Properties.TryGetValue(name, out var value) ? value : string.Empty;
    }

    public sealed class ColumnMetadataInfo
    {
        public ColumnMetadataInfo(string logicalName, IDictionary<string, string> properties)
        {
            LogicalName = logicalName ?? throw new ArgumentNullException(nameof(logicalName));
            Properties = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(properties ?? throw new ArgumentNullException(nameof(properties)), StringComparer.Ordinal));
        }

        public string LogicalName { get; }

        public IReadOnlyDictionary<string, string> Properties { get; }

        public string DisplayName => GetProperty("Display name");

        public string GetProperty(string name) => Properties.TryGetValue(name, out var value) ? value : string.Empty;
    }

    public enum ComparisonScope
    {
        Table,
        Column,
        Form,
        View,
        Report
    }

    public enum DifferenceKind
    {
        MissingInEnvironmentB,
        MissingInEnvironmentA,
        Changed
    }

    public enum DifferenceSeverity
    {
        Critical = 0,
        High = 1,
        Medium = 2,
        Low = 3
    }

    public sealed class ComparisonIssue
    {
        public ComparisonIssue(
            DifferenceSeverity severity,
            ComparisonScope scope,
            DifferenceKind kind,
            string tableLogicalName,
            string tableDisplayName,
            string componentKey,
            string componentName,
            string propertyName,
            string environmentAValue,
            string environmentBValue,
            string details,
            string tableClassification = "",
            string? environmentAPreviewValue = null,
            string? environmentBPreviewValue = null,
            string? environmentAComponentKey = null,
            string? environmentBComponentKey = null)
        {
            Severity = severity;
            Scope = scope;
            Kind = kind;
            TableLogicalName = tableLogicalName ?? string.Empty;
            TableDisplayName = tableDisplayName ?? string.Empty;
            TableClassification = tableClassification ?? string.Empty;
            ComponentKey = componentKey ?? string.Empty;
            ComponentName = componentName ?? string.Empty;
            PropertyName = propertyName ?? string.Empty;
            EnvironmentAValue = environmentAValue ?? string.Empty;
            EnvironmentBValue = environmentBValue ?? string.Empty;
            EnvironmentAPreviewValue = environmentAPreviewValue ?? EnvironmentAValue;
            EnvironmentBPreviewValue = environmentBPreviewValue ?? EnvironmentBValue;
            EnvironmentAComponentKey = environmentAComponentKey ?? ComponentKey;
            EnvironmentBComponentKey = environmentBComponentKey ?? ComponentKey;
            Details = details ?? string.Empty;
        }

        public DifferenceSeverity Severity { get; }

        public ComparisonScope Scope { get; }

        public DifferenceKind Kind { get; }

        public string TableLogicalName { get; }

        public string TableDisplayName { get; }

        public string TableClassification { get; }

        public string ComponentKey { get; }

        public string ComponentName { get; }

        public string PropertyName { get; }

        public string EnvironmentAValue { get; }

        public string EnvironmentBValue { get; }

        public string EnvironmentAPreviewValue { get; }

        public string EnvironmentBPreviewValue { get; }

        public string EnvironmentAComponentKey { get; }

        public string EnvironmentBComponentKey { get; }

        public string Details { get; }
    }

    public sealed class MetadataComparisonResult
    {
        public MetadataComparisonResult(
            EnvironmentMetadataSnapshot environmentA,
            EnvironmentMetadataSnapshot environmentB,
            IEnumerable<ComparisonIssue> issues)
        {
            EnvironmentA = environmentA ?? throw new ArgumentNullException(nameof(environmentA));
            EnvironmentB = environmentB ?? throw new ArgumentNullException(nameof(environmentB));
            Issues = new ReadOnlyCollection<ComparisonIssue>(
                (issues ?? throw new ArgumentNullException(nameof(issues)))
                    .OrderBy(issue => issue.Severity)
                    .ThenBy(issue => issue.TableLogicalName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(issue => issue.ComponentKey, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(issue => issue.PropertyName, StringComparer.OrdinalIgnoreCase)
                    .ToList());
        }

        public EnvironmentMetadataSnapshot EnvironmentA { get; }

        public EnvironmentMetadataSnapshot EnvironmentB { get; }

        public IReadOnlyList<ComparisonIssue> Issues { get; }
    }
}
