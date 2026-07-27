using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using EnvironmentComparison.Domain;

namespace EnvironmentComparison.Services
{
    public sealed class MetadataComparisonService
    {
        private static readonly IReadOnlyDictionary<string, DifferenceSeverity> TablePropertySeverity =
            new Dictionary<string, DifferenceSeverity>(StringComparer.Ordinal)
            {
                ["Table classification"] = DifferenceSeverity.Critical,
                ["Schema name"] = DifferenceSeverity.High,
                ["Display name"] = DifferenceSeverity.Low,
                ["Display collection name"] = DifferenceSeverity.Low,
                ["Description"] = DifferenceSeverity.Low,
                ["Entity set name"] = DifferenceSeverity.High,
                ["Ownership type"] = DifferenceSeverity.Critical,
                ["Primary ID column"] = DifferenceSeverity.Critical,
                ["Primary name column"] = DifferenceSeverity.High,
                ["Activity table"] = DifferenceSeverity.Critical,
                ["Activity party table"] = DifferenceSeverity.Critical,
                ["Audit enabled"] = DifferenceSeverity.High,
                ["Change tracking enabled"] = DifferenceSeverity.High,
                ["Business process enabled"] = DifferenceSeverity.Medium,
                ["Connections enabled"] = DifferenceSeverity.Medium,
                ["Document management enabled"] = DifferenceSeverity.Medium,
                ["Duplicate detection enabled"] = DifferenceSeverity.Medium,
                ["Mail merge enabled"] = DifferenceSeverity.Low,
                ["Quick create enabled"] = DifferenceSeverity.Medium,
                ["Valid for advanced find"] = DifferenceSeverity.Medium,
                ["Valid for queues"] = DifferenceSeverity.Medium,
                ["SLA enabled"] = DifferenceSeverity.Medium,
                ["Has activities"] = DifferenceSeverity.Medium,
                ["Has notes"] = DifferenceSeverity.Medium
            };

        private static readonly IReadOnlyDictionary<string, DifferenceSeverity> ColumnPropertySeverity =
            new Dictionary<string, DifferenceSeverity>(StringComparer.Ordinal)
            {
                ["Schema name"] = DifferenceSeverity.High,
                ["Display name"] = DifferenceSeverity.Low,
                ["Description"] = DifferenceSeverity.Low,
                ["Attribute type"] = DifferenceSeverity.Critical,
                ["Attribute type name"] = DifferenceSeverity.Critical,
                ["Requirement level"] = DifferenceSeverity.Critical,
                ["Primary ID"] = DifferenceSeverity.Critical,
                ["Primary name"] = DifferenceSeverity.High,
                ["Logical column"] = DifferenceSeverity.High,
                ["Audit enabled"] = DifferenceSeverity.High,
                ["Field security enabled"] = DifferenceSeverity.High,
                ["Valid for create"] = DifferenceSeverity.High,
                ["Valid for read"] = DifferenceSeverity.High,
                ["Valid for update"] = DifferenceSeverity.High,
                ["Valid for advanced find"] = DifferenceSeverity.Medium,
                ["Valid for forms"] = DifferenceSeverity.Medium,
                ["Valid for grids"] = DifferenceSeverity.Medium,
                ["Source type"] = DifferenceSeverity.High,
                ["Attribute of"] = DifferenceSeverity.High,
                ["Autonumber format"] = DifferenceSeverity.Critical,
                ["Format"] = DifferenceSeverity.High,
                ["Format name"] = DifferenceSeverity.High,
                ["Maximum length"] = DifferenceSeverity.Critical,
                ["Minimum value"] = DifferenceSeverity.High,
                ["Maximum value"] = DifferenceSeverity.High,
                ["Precision"] = DifferenceSeverity.High,
                ["Precision source"] = DifferenceSeverity.High,
                ["Date/time behavior"] = DifferenceSeverity.Critical,
                ["Can change date/time behavior"] = DifferenceSeverity.Medium,
                ["Lookup targets"] = DifferenceSeverity.Critical,
                ["Default value"] = DifferenceSeverity.High,
                ["Choice set name"] = DifferenceSeverity.High,
                ["Global choice"] = DifferenceSeverity.High,
                ["Choice values"] = DifferenceSeverity.Critical,
                ["File maximum size (KB)"] = DifferenceSeverity.High,
                ["Image maximum height"] = DifferenceSeverity.High,
                ["Image maximum width"] = DifferenceSeverity.High,
                ["Store full image"] = DifferenceSeverity.High,
                ["Formula definition"] = DifferenceSeverity.Critical
            };

        private static readonly IReadOnlyDictionary<string, DifferenceSeverity> FormPropertySeverity =
            new Dictionary<string, DifferenceSeverity>(StringComparer.Ordinal)
            {
                ["Name"] = DifferenceSeverity.Low,
                ["Description"] = DifferenceSeverity.Low,
                ["Form type"] = DifferenceSeverity.High,
                ["Activation state"] = DifferenceSeverity.High,
                ["Presentation"] = DifferenceSeverity.Medium,
                ["Form XML"] = DifferenceSeverity.Critical
            };

        private static readonly IReadOnlyDictionary<string, DifferenceSeverity> ViewPropertySeverity =
            new Dictionary<string, DifferenceSeverity>(StringComparer.Ordinal)
            {
                ["Name"] = DifferenceSeverity.Low,
                ["Query type"] = DifferenceSeverity.High,
                ["Default view"] = DifferenceSeverity.High,
                ["Quick find view"] = DifferenceSeverity.High,
                ["Fetch XML"] = DifferenceSeverity.Critical,
                ["Layout XML"] = DifferenceSeverity.Critical,
                ["Column set XML"] = DifferenceSeverity.High,
                ["Advanced group by"] = DifferenceSeverity.High
            };

        private static readonly HashSet<string> DefinitionProperties =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Formula definition",
                "Form XML",
                "Fetch XML",
                "Layout XML",
                "Column set XML"
            };

        public MetadataComparisonResult Compare(
            EnvironmentMetadataSnapshot environmentA,
            EnvironmentMetadataSnapshot environmentB)
        {
            if (environmentA == null) throw new ArgumentNullException(nameof(environmentA));
            if (environmentB == null) throw new ArgumentNullException(nameof(environmentB));

            var issues = new List<ComparisonIssue>();
            var tablesA = environmentA.Tables.ToDictionary(table => table.LogicalName, StringComparer.OrdinalIgnoreCase);
            var tablesB = environmentB.Tables.ToDictionary(table => table.LogicalName, StringComparer.OrdinalIgnoreCase);
            var selectedAreas = environmentA.IncludedAreas & environmentB.IncludedAreas;

            if ((selectedAreas & (ComparisonAreas.TableMetadata | ComparisonAreas.Columns)) != 0)
            {
                foreach (var logicalName in tablesA.Keys.Union(tablesB.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                {
                    var hasA = tablesA.TryGetValue(logicalName, out var tableA);
                    var hasB = tablesB.TryGetValue(logicalName, out var tableB);
                    if (!hasA)
                    {
                        if ((selectedAreas & ComparisonAreas.TableMetadata) != 0)
                        {
                            issues.Add(PresenceIssue(tableB!, DifferenceKind.MissingInEnvironmentA));
                        }

                        if ((selectedAreas & ComparisonAreas.Columns) != 0)
                        {
                            AddAllColumnPresenceIssues(tableB!, DifferenceKind.MissingInEnvironmentA, issues);
                        }

                        continue;
                    }

                    if (!hasB)
                    {
                        if ((selectedAreas & ComparisonAreas.TableMetadata) != 0)
                        {
                            issues.Add(PresenceIssue(tableA!, DifferenceKind.MissingInEnvironmentB));
                        }

                        if ((selectedAreas & ComparisonAreas.Columns) != 0)
                        {
                            AddAllColumnPresenceIssues(tableA!, DifferenceKind.MissingInEnvironmentB, issues);
                        }

                        continue;
                    }

                    if ((selectedAreas & ComparisonAreas.TableMetadata) != 0)
                    {
                        CompareProperties(tableA!, tableB!, issues);
                    }

                    if ((selectedAreas & ComparisonAreas.Columns) != 0)
                    {
                        CompareColumns(tableA!, tableB!, issues);
                    }
                }
            }

            if ((selectedAreas & ComparisonAreas.Forms) != 0)
            {
                CompareForms(environmentA, environmentB, tablesA, tablesB, issues);
            }

            if ((selectedAreas & ComparisonAreas.Views) != 0)
            {
                CompareViews(environmentA, environmentB, tablesA, tablesB, issues);
            }

            return new MetadataComparisonResult(environmentA, environmentB, issues);
        }

        private static void AddAllColumnPresenceIssues(
            TableMetadataInfo table,
            DifferenceKind kind,
            ICollection<ComparisonIssue> issues)
        {
            foreach (var column in table.Columns)
            {
                var missingInB = kind == DifferenceKind.MissingInEnvironmentB;
                issues.Add(ColumnPresenceIssue(
                    table,
                    column,
                    kind,
                    missingInB ? DifferenceSeverity.Critical : DifferenceSeverity.Medium,
                    null));
            }
        }

        private static void CompareForms(
            EnvironmentMetadataSnapshot environmentA,
            EnvironmentMetadataSnapshot environmentB,
            IReadOnlyDictionary<string, TableMetadataInfo> tablesA,
            IReadOnlyDictionary<string, TableMetadataInfo> tablesB,
            ICollection<ComparisonIssue> issues)
        {
            var formsA = environmentA.Forms.ToDictionary(form => form.Key, StringComparer.OrdinalIgnoreCase);
            var formsB = environmentB.Forms.ToDictionary(form => form.Key, StringComparer.OrdinalIgnoreCase);
            foreach (var key in formsA.Keys.Union(formsB.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var hasA = formsA.TryGetValue(key, out var formA);
                var hasB = formsB.TryGetValue(key, out var formB);
                if (!hasA || !hasB)
                {
                    var form = formA ?? formB!;
                    var missingInB = hasA;
                    issues.Add(new ComparisonIssue(
                        missingInB ? DifferenceSeverity.High : DifferenceSeverity.Medium,
                        ComparisonScope.Form,
                        missingInB ? DifferenceKind.MissingInEnvironmentB : DifferenceKind.MissingInEnvironmentA,
                        form.TableLogicalName,
                        TableDisplayName(form.TableLogicalName, tablesA, tablesB),
                        form.Key,
                        form.Name,
                        "Form presence",
                        missingInB ? "Present" : "Missing",
                        missingInB ? "Missing" : "Present",
                        missingInB
                            ? "The form exists in Environment A but is missing from Environment B."
                            : "The form exists in Environment B but is missing from Environment A.",
                        TableClassification(form.TableLogicalName, tablesA, tablesB)));
                    continue;
                }

                CompareComponentProperties(
                    ComparisonScope.Form,
                    formA!.TableLogicalName,
                    TableDisplayName(formA.TableLogicalName, tablesA, tablesB),
                    TableClassification(formA.TableLogicalName, tablesA, tablesB),
                    formA.Key,
                    Prefer(formA.Name, formB!.Name),
                    formA.GetProperty,
                    formB.GetProperty,
                    FormPropertySeverity,
                    issues);
            }
        }

        private static void CompareViews(
            EnvironmentMetadataSnapshot environmentA,
            EnvironmentMetadataSnapshot environmentB,
            IReadOnlyDictionary<string, TableMetadataInfo> tablesA,
            IReadOnlyDictionary<string, TableMetadataInfo> tablesB,
            ICollection<ComparisonIssue> issues)
        {
            var viewsA = environmentA.Views.ToDictionary(view => view.Key, StringComparer.OrdinalIgnoreCase);
            var viewsB = environmentB.Views.ToDictionary(view => view.Key, StringComparer.OrdinalIgnoreCase);
            foreach (var key in viewsA.Keys.Union(viewsB.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var hasA = viewsA.TryGetValue(key, out var viewA);
                var hasB = viewsB.TryGetValue(key, out var viewB);
                if (!hasA || !hasB)
                {
                    var view = viewA ?? viewB!;
                    var missingInB = hasA;
                    issues.Add(new ComparisonIssue(
                        missingInB ? DifferenceSeverity.High : DifferenceSeverity.Medium,
                        ComparisonScope.View,
                        missingInB ? DifferenceKind.MissingInEnvironmentB : DifferenceKind.MissingInEnvironmentA,
                        view.TableLogicalName,
                        TableDisplayName(view.TableLogicalName, tablesA, tablesB),
                        view.Key,
                        view.Name,
                        "View presence",
                        missingInB ? "Present" : "Missing",
                        missingInB ? "Missing" : "Present",
                        missingInB
                            ? "The system view exists in Environment A but is missing from Environment B."
                            : "The system view exists in Environment B but is missing from Environment A.",
                        TableClassification(view.TableLogicalName, tablesA, tablesB)));
                    continue;
                }

                CompareComponentProperties(
                    ComparisonScope.View,
                    viewA!.TableLogicalName,
                    TableDisplayName(viewA.TableLogicalName, tablesA, tablesB),
                    TableClassification(viewA.TableLogicalName, tablesA, tablesB),
                    viewA.Key,
                    Prefer(viewA.Name, viewB!.Name),
                    viewA.GetProperty,
                    viewB.GetProperty,
                    ViewPropertySeverity,
                    issues);
            }
        }

        private static void CompareComponentProperties(
            ComparisonScope scope,
            string tableLogicalName,
            string tableDisplayName,
            string tableClassification,
            string componentKey,
            string componentName,
            Func<string, string> valueA,
            Func<string, string> valueB,
            IEnumerable<KeyValuePair<string, DifferenceSeverity>> properties,
            ICollection<ComparisonIssue> issues)
        {
            foreach (var property in properties)
            {
                var first = valueA(property.Key);
                var second = valueB(property.Key);
                if (EqualProperty(property.Key, first, second))
                {
                    continue;
                }

                var definition = IsDefinitionProperty(property.Key);

                issues.Add(new ComparisonIssue(
                    property.Value,
                    scope,
                    DifferenceKind.Changed,
                    tableLogicalName,
                    tableDisplayName,
                    componentKey,
                    componentName,
                    property.Key,
                    first,
                    second,
                    $"{scope} setting '{property.Key}' is different.",
                    tableClassification,
                    definition ? DataverseMetadataService.DefinitionFingerprint(first) : null,
                    definition ? DataverseMetadataService.DefinitionFingerprint(second) : null));
            }
        }

        private static string TableDisplayName(
            string logicalName,
            IReadOnlyDictionary<string, TableMetadataInfo> tablesA,
            IReadOnlyDictionary<string, TableMetadataInfo> tablesB)
        {
            return tablesA.TryGetValue(logicalName, out var tableA)
                ? tableA.DisplayName
                : tablesB.TryGetValue(logicalName, out var tableB)
                    ? tableB.DisplayName
                    : string.Empty;
        }

        private static string TableClassification(
            string logicalName,
            IReadOnlyDictionary<string, TableMetadataInfo> tablesA,
            IReadOnlyDictionary<string, TableMetadataInfo> tablesB)
        {
            var classificationA = tablesA.TryGetValue(logicalName, out var tableA)
                ? tableA.Classification
                : string.Empty;
            var classificationB = tablesB.TryGetValue(logicalName, out var tableB)
                ? tableB.Classification
                : string.Empty;
            return CombinedClassification(classificationA, classificationB);
        }

        private static ComparisonIssue PresenceIssue(TableMetadataInfo table, DifferenceKind kind)
        {
            var missingInB = kind == DifferenceKind.MissingInEnvironmentB;
            return new ComparisonIssue(
                missingInB ? DifferenceSeverity.Critical : DifferenceSeverity.Medium,
                ComparisonScope.Table,
                kind,
                table.LogicalName,
                table.DisplayName,
                string.Empty,
                string.Empty,
                "Table presence",
                missingInB ? "Present" : "Missing",
                missingInB ? "Missing" : "Present",
                missingInB
                    ? "The table exists in Environment A but is missing from Environment B."
                    : "The table exists in Environment B but is missing from Environment A.",
                table.Classification);
        }

        private static void CompareProperties(
            TableMetadataInfo tableA,
            TableMetadataInfo tableB,
            ICollection<ComparisonIssue> issues)
        {
            foreach (var property in TablePropertySeverity)
            {
                var valueA = tableA.GetProperty(property.Key);
                var valueB = tableB.GetProperty(property.Key);
                if (Equal(valueA, valueB))
                {
                    continue;
                }

                issues.Add(new ComparisonIssue(
                    property.Value,
                    ComparisonScope.Table,
                    DifferenceKind.Changed,
                    tableA.LogicalName,
                    Prefer(tableA.DisplayName, tableB.DisplayName),
                    string.Empty,
                    string.Empty,
                    property.Key,
                    valueA,
                    valueB,
                    $"Table setting '{property.Key}' is different.",
                    CombinedClassification(tableA.Classification, tableB.Classification)));
            }
        }

        private static void CompareColumns(
            TableMetadataInfo tableA,
            TableMetadataInfo tableB,
            ICollection<ComparisonIssue> issues)
        {
            var columnsA = tableA.Columns.ToDictionary(column => column.LogicalName, StringComparer.OrdinalIgnoreCase);
            var columnsB = tableB.Columns.ToDictionary(column => column.LogicalName, StringComparer.OrdinalIgnoreCase);
            var missingFromB = columnsA.Values.Where(column => !columnsB.ContainsKey(column.LogicalName)).ToList();
            var missingFromA = columnsB.Values.Where(column => !columnsA.ContainsKey(column.LogicalName)).ToList();

            foreach (var column in missingFromB)
            {
                var rename = FindRenameCandidate(column, missingFromA);
                var required = column.GetProperty("Requirement level");
                var primary = IsTrue(column.GetProperty("Primary ID")) || IsTrue(column.GetProperty("Primary name"));
                var severity = primary || (!string.IsNullOrEmpty(required) && !required.Equals("None", StringComparison.OrdinalIgnoreCase))
                    ? DifferenceSeverity.Critical
                    : DifferenceSeverity.High;
                issues.Add(ColumnPresenceIssue(tableA, column, DifferenceKind.MissingInEnvironmentB, severity, rename));
            }

            foreach (var column in missingFromA)
            {
                var rename = FindRenameCandidate(column, missingFromB);
                issues.Add(ColumnPresenceIssue(tableB, column, DifferenceKind.MissingInEnvironmentA, DifferenceSeverity.Medium, rename));
            }

            foreach (var logicalName in columnsA.Keys.Intersect(columnsB.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var columnA = columnsA[logicalName];
                var columnB = columnsB[logicalName];
                foreach (var property in ColumnPropertySeverity)
                {
                    var valueA = columnA.GetProperty(property.Key);
                    var valueB = columnB.GetProperty(property.Key);
                    if (EqualProperty(property.Key, valueA, valueB))
                    {
                        continue;
                    }

                    var definition = IsDefinitionProperty(property.Key);

                    issues.Add(new ComparisonIssue(
                        property.Value,
                        ComparisonScope.Column,
                        DifferenceKind.Changed,
                        tableA.LogicalName,
                        Prefer(tableA.DisplayName, tableB.DisplayName),
                        columnA.LogicalName,
                        Prefer(columnA.DisplayName, columnB.DisplayName),
                        property.Key,
                        valueA,
                        valueB,
                        property.Key == "Display name"
                            ? "The column display name was renamed. Logical names still match."
                            : $"Column setting '{property.Key}' is different.",
                        CombinedClassification(tableA.Classification, tableB.Classification),
                        definition ? DataverseMetadataService.DefinitionFingerprint(valueA) : null,
                        definition ? DataverseMetadataService.DefinitionFingerprint(valueB) : null));
                }
            }
        }

        private static ComparisonIssue ColumnPresenceIssue(
            TableMetadataInfo table,
            ColumnMetadataInfo column,
            DifferenceKind kind,
            DifferenceSeverity severity,
            ColumnMetadataInfo? possibleRename)
        {
            var missingInB = kind == DifferenceKind.MissingInEnvironmentB;
            var renameText = possibleRename == null
                ? string.Empty
                : $" Possible renamed/recreated column: '{possibleRename.LogicalName}' ({possibleRename.DisplayName}).";
            return new ComparisonIssue(
                severity,
                ComparisonScope.Column,
                kind,
                table.LogicalName,
                table.DisplayName,
                column.LogicalName,
                column.DisplayName,
                "Column presence",
                missingInB ? "Present" : "Missing",
                missingInB ? "Missing" : "Present",
                (missingInB
                    ? "The column exists in Environment A but is missing from Environment B."
                    : "The column exists in Environment B but is missing from Environment A.") + renameText,
                table.Classification);
        }

        private static ColumnMetadataInfo? FindRenameCandidate(
            ColumnMetadataInfo source,
            IEnumerable<ColumnMetadataInfo> candidates)
        {
            return candidates
                .Select(candidate => new { Candidate = candidate, Score = RenameScore(source, candidate) })
                .Where(item => item.Score >= 4)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Candidate.LogicalName, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.Candidate)
                .FirstOrDefault();
        }

        private static int RenameScore(ColumnMetadataInfo first, ColumnMetadataInfo second)
        {
            var score = 0;
            if (Equal(first.GetProperty("Attribute type"), second.GetProperty("Attribute type"))) score += 1;
            if (Equal(first.GetProperty("Display name"), second.GetProperty("Display name"))
                && !string.IsNullOrWhiteSpace(first.DisplayName)) score += 3;
            if (Equal(first.GetProperty("Requirement level"), second.GetProperty("Requirement level"))) score += 1;
            if (Equal(first.GetProperty("Maximum length"), second.GetProperty("Maximum length"))
                && !string.IsNullOrWhiteSpace(first.GetProperty("Maximum length"))) score += 1;
            return score;
        }

        private static bool Equal(string first, string second) =>
            string.Equals(first ?? string.Empty, second ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        private static bool EqualProperty(string propertyName, string first, string second)
        {
            var comparisonA = ComparisonValue(propertyName, first);
            var comparisonB = ComparisonValue(propertyName, second);
            return string.Equals(
                comparisonA,
                comparisonB,
                IsDefinitionProperty(propertyName) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }

        internal static string ComparisonValue(string propertyName, string value)
        {
            var safeValue = value ?? string.Empty;
            if (!propertyName.Equals("Layout XML", StringComparison.Ordinal) || safeValue.Length == 0)
            {
                return safeValue;
            }

            try
            {
                var document = XDocument.Parse(safeValue, LoadOptions.None);
                if (document.Root?.Name.LocalName == "grid")
                {
                    document.Root.Attribute("object")?.Remove();
                }

                return document.ToString(SaveOptions.DisableFormatting);
            }
            catch
            {
                return safeValue;
            }
        }

        internal static bool IsDefinitionProperty(string propertyName) => DefinitionProperties.Contains(propertyName);

        private static string CombinedClassification(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first)) return second;
            if (string.IsNullOrWhiteSpace(second) || Equal(first, second)) return first;
            return $"{first} / {second}";
        }

        private static bool IsTrue(string value) => value.Equals("True", StringComparison.OrdinalIgnoreCase);

        private static string Prefer(string first, string second) => !string.IsNullOrWhiteSpace(first) ? first : second;
    }
}
