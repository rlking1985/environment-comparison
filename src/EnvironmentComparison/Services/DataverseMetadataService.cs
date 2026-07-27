using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using EnvironmentComparison.Domain;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace EnvironmentComparison.Services
{
    public sealed class DataverseMetadataService
    {
        private static readonly KeyValuePair<string, string>[] TableProperties =
        {
            Property("Schema name", "SchemaName"),
            Property("Display name", "DisplayName"),
            Property("Display collection name", "DisplayCollectionName"),
            Property("Description", "Description"),
            Property("Entity set name", "EntitySetName"),
            Property("Ownership type", "OwnershipType"),
            Property("Primary ID column", "PrimaryIdAttribute"),
            Property("Primary name column", "PrimaryNameAttribute"),
            Property("Activity table", "IsActivity"),
            Property("Activity party table", "IsActivityParty"),
            Property("Audit enabled", "IsAuditEnabled"),
            Property("Change tracking enabled", "ChangeTrackingEnabled"),
            Property("Business process enabled", "IsBusinessProcessEnabled"),
            Property("Connections enabled", "IsConnectionsEnabled"),
            Property("Document management enabled", "IsDocumentManagementEnabled"),
            Property("Duplicate detection enabled", "IsDuplicateDetectionEnabled"),
            Property("Mail merge enabled", "IsMailMergeEnabled"),
            Property("Quick create enabled", "IsQuickCreateEnabled"),
            Property("Valid for advanced find", "IsValidForAdvancedFind"),
            Property("Valid for queues", "IsValidForQueue"),
            Property("SLA enabled", "IsSLAEnabled"),
            Property("Has activities", "HasActivities"),
            Property("Has notes", "HasNotes")
        };

        private static readonly KeyValuePair<string, string>[] ColumnProperties =
        {
            Property("Schema name", "SchemaName"),
            Property("Display name", "DisplayName"),
            Property("Description", "Description"),
            Property("Attribute type", "AttributeType"),
            Property("Attribute type name", "AttributeTypeName"),
            Property("Requirement level", "RequiredLevel"),
            Property("Primary ID", "IsPrimaryId"),
            Property("Primary name", "IsPrimaryName"),
            Property("Logical column", "IsLogical"),
            Property("Audit enabled", "IsAuditEnabled"),
            Property("Field security enabled", "IsSecured"),
            Property("Valid for create", "IsValidForCreate"),
            Property("Valid for read", "IsValidForRead"),
            Property("Valid for update", "IsValidForUpdate"),
            Property("Valid for advanced find", "IsValidForAdvancedFind"),
            Property("Valid for forms", "IsValidForForm"),
            Property("Valid for grids", "IsValidForGrid"),
            Property("Source type", "SourceType"),
            Property("Attribute of", "AttributeOf"),
            Property("Autonumber format", "AutoNumberFormat"),
            Property("Format", "Format"),
            Property("Format name", "FormatName"),
            Property("Maximum length", "MaxLength"),
            Property("Minimum value", "MinValue"),
            Property("Maximum value", "MaxValue"),
            Property("Precision", "Precision"),
            Property("Precision source", "PrecisionSource"),
            Property("Date/time behavior", "DateTimeBehavior"),
            Property("Can change date/time behavior", "CanChangeDateTimeBehavior"),
            Property("Lookup targets", "Targets"),
            Property("Default value", "DefaultValue"),
            Property("File maximum size (KB)", "MaxSizeInKB"),
            Property("Image maximum height", "MaxHeight"),
            Property("Image maximum width", "MaxWidth"),
            Property("Store full image", "CanStoreFullImage"),
            Property("Formula definition", "FormulaDefinition")
        };

        public EnvironmentMetadataSnapshot LoadSnapshot(
            IOrganizationService service,
            ComparisonAreas areas,
            bool retrieveAsIfPublished,
            Action<int, string>? reportProgress = null)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (areas == ComparisonAreas.None) throw new ArgumentException("At least one comparison area is required.", nameof(areas));

            reportProgress?.Invoke(5, "Loading table metadata...");
            var filters = EntityFilters.Entity;
            if ((areas & ComparisonAreas.Columns) != 0)
            {
                filters |= EntityFilters.Attributes;
            }

            var response = (RetrieveAllEntitiesResponse)service.Execute(new RetrieveAllEntitiesRequest
            {
                EntityFilters = filters,
                RetrieveAsIfPublished = retrieveAsIfPublished
            });

            var entityMetadata = response.EntityMetadata ?? Array.Empty<EntityMetadata>();
            var tableNamesByObjectType = entityMetadata
                .Where(entity => entity.ObjectTypeCode.HasValue && !string.IsNullOrWhiteSpace(entity.LogicalName))
                .GroupBy(entity => entity.ObjectTypeCode!.Value)
                .ToDictionary(group => group.Key, group => group.First().LogicalName);
            var tables = entityMetadata
                .Where(entity => !string.IsNullOrWhiteSpace(entity.LogicalName))
                .Select(entity => ToTable(entity, (areas & ComparisonAreas.Columns) != 0))
                .ToList();

            var forms = new List<FormMetadataInfo>();
            if ((areas & ComparisonAreas.Forms) != 0)
            {
                reportProgress?.Invoke(60, "Loading system forms...");
                forms.AddRange(LoadForms(service, tableNamesByObjectType));
            }

            var views = new List<ViewMetadataInfo>();
            if ((areas & ComparisonAreas.Views) != 0)
            {
                reportProgress?.Invoke(80, "Loading system views...");
                views.AddRange(LoadViews(service, tableNamesByObjectType));
            }

            reportProgress?.Invoke(100, "Metadata snapshot loaded.");
            return new EnvironmentMetadataSnapshot(tables, forms, views, areas);
        }

        private static TableMetadataInfo ToTable(EntityMetadata metadata, bool includeColumns)
        {
            var properties = ReadProperties(metadata, TableProperties);
            properties["Table classification"] = ClassifyTable(metadata);
            var columns = includeColumns
                ? (metadata.Attributes ?? Array.Empty<AttributeMetadata>())
                    .Where(attribute => !string.IsNullOrWhiteSpace(attribute.LogicalName))
                    .Select(ToColumn)
                : Enumerable.Empty<ColumnMetadataInfo>();
            return new TableMetadataInfo(metadata.LogicalName, properties, columns);
        }

        private static ColumnMetadataInfo ToColumn(AttributeMetadata metadata)
        {
            var properties = ReadProperties(metadata, ColumnProperties);
            properties["Formula definition"] = NormalizeDefinition(properties["Formula definition"]);
            var optionSet = ReadRawProperty(metadata, "OptionSet");
            if (optionSet != null)
            {
                properties["Choice set name"] = FormatValue(ReadRawProperty(optionSet, "Name"));
                properties["Global choice"] = FormatValue(ReadRawProperty(optionSet, "IsGlobal"));
                properties["Choice values"] = FormatChoiceValues(optionSet);
            }

            return new ColumnMetadataInfo(metadata.LogicalName, properties);
        }

        private static IEnumerable<FormMetadataInfo> LoadForms(
            IOrganizationService service,
            IReadOnlyDictionary<int, string> tableNamesByObjectType)
        {
            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet(
                    "formid",
                    "formidunique",
                    "name",
                    "description",
                    "type",
                    "formxml",
                    "objecttypecode",
                    "formactivationstate",
                    "formpresentation",
                    "uniquename")
            };

            foreach (var entity in RetrieveAll(service, query))
            {
                var table = ResolveTableName(entity, "objecttypecode", tableNamesByObjectType);
                if (string.IsNullOrWhiteSpace(table))
                {
                    continue;
                }

                var name = EntityValue(entity, "name");
                var uniqueName = EntityValue(entity, "uniquename");
                var stableId = EntityGuid(entity, "formidunique") ?? entity.Id;
                var key = !string.IsNullOrWhiteSpace(uniqueName)
                    ? $"{table}|unique:{uniqueName.Trim().ToLowerInvariant()}"
                    : $"{table}|id:{stableId:D}";
                var properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Name"] = name,
                    ["Description"] = EntityValue(entity, "description"),
                    ["Form type"] = EntityValue(entity, "type"),
                    ["Activation state"] = EntityValue(entity, "formactivationstate"),
                    ["Presentation"] = EntityValue(entity, "formpresentation"),
                    ["Form XML"] = NormalizeDefinition(EntityValue(entity, "formxml"))
                };
                yield return new FormMetadataInfo(key, table, name, properties);
            }
        }

        private static IEnumerable<ViewMetadataInfo> LoadViews(
            IOrganizationService service,
            IReadOnlyDictionary<int, string> tableNamesByObjectType)
        {
            var query = new QueryExpression("savedquery")
            {
                ColumnSet = new ColumnSet(
                    "savedqueryid",
                    "name",
                    "returnedtypecode",
                    "querytype",
                    "fetchxml",
                    "layoutxml",
                    "columnsetxml",
                    "isdefault",
                    "isquickfindquery",
                    "advancedgroupby")
            };

            foreach (var entity in RetrieveAll(service, query))
            {
                var table = ResolveTableName(entity, "returnedtypecode", tableNamesByObjectType);
                if (string.IsNullOrWhiteSpace(table))
                {
                    continue;
                }

                var name = EntityValue(entity, "name");
                var key = $"{table}|id:{entity.Id:D}";
                var properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Name"] = name,
                    ["Query type"] = EntityValue(entity, "querytype"),
                    ["Default view"] = EntityValue(entity, "isdefault"),
                    ["Quick find view"] = EntityValue(entity, "isquickfindquery"),
                    ["Fetch XML"] = NormalizeDefinition(EntityValue(entity, "fetchxml")),
                    ["Layout XML"] = NormalizeDefinition(EntityValue(entity, "layoutxml")),
                    ["Column set XML"] = NormalizeDefinition(EntityValue(entity, "columnsetxml")),
                    ["Advanced group by"] = EntityValue(entity, "advancedgroupby")
                };
                yield return new ViewMetadataInfo(key, table, name, properties);
            }
        }

        private static List<Entity> RetrieveAll(IOrganizationService service, QueryExpression query)
        {
            var result = new List<Entity>();
            query.PageInfo = new PagingInfo { Count = 5000, PageNumber = 1 };
            while (true)
            {
                var page = service.RetrieveMultiple(query);
                result.AddRange(page.Entities);
                if (!page.MoreRecords)
                {
                    return result;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }
        }

        private static Dictionary<string, string> ReadProperties(
            object metadata,
            IEnumerable<KeyValuePair<string, string>> propertyMap)
        {
            return propertyMap.ToDictionary(
                property => property.Key,
                property => FormatValue(ReadRawProperty(metadata, property.Value)),
                StringComparer.Ordinal);
        }

        private static object? ReadRawProperty(object instance, string propertyName)
        {
            return instance.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(instance, null);
        }

        private static string FormatValue(object? value)
        {
            if (value == null) return string.Empty;
            if (value is string text) return text.Trim();
            if (value is Label label)
            {
                return label.UserLocalizedLabel?.Label
                    ?? label.LocalizedLabels?.FirstOrDefault()?.Label
                    ?? string.Empty;
            }

            if (value is OptionSetValue option) return option.Value.ToString(CultureInfo.InvariantCulture);
            if (value is EntityReference reference) return reference.Id.ToString("D");
            if (value is Guid guid) return guid.ToString("D");
            if (value is bool boolean) return boolean ? "True" : "False";
            if (value is Enum) return value.ToString() ?? string.Empty;
            if (value is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;

            var nestedValue = ReadRawProperty(value, "Value");
            if (nestedValue != null && !ReferenceEquals(nestedValue, value))
            {
                return FormatValue(nestedValue);
            }

            if (value is IEnumerable enumerable)
            {
                return string.Join(" | ", enumerable.Cast<object>().Select(FormatValue).OrderBy(item => item, StringComparer.OrdinalIgnoreCase));
            }

            return value.ToString()?.Trim() ?? string.Empty;
        }

        private static string FormatChoiceValues(object optionSet)
        {
            var rawOptions = ReadRawProperty(optionSet, "Options") as IEnumerable;
            var options = rawOptions?.Cast<object>().ToList() ?? new List<object>();
            if (options.Count == 0)
            {
                var falseOption = ReadRawProperty(optionSet, "FalseOption");
                var trueOption = ReadRawProperty(optionSet, "TrueOption");
                if (falseOption != null) options.Add(falseOption);
                if (trueOption != null) options.Add(trueOption);
            }

            return string.Join(" | ", options
                .Select(option => new
                {
                    Value = FormatValue(ReadRawProperty(option, "Value")),
                    Label = FormatValue(ReadRawProperty(option, "Label"))
                })
                .OrderBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
                .Select(option => $"{option.Value}:{option.Label}"));
        }

        private static string ResolveTableName(
            Entity entity,
            string attributeName,
            IReadOnlyDictionary<int, string> tableNamesByObjectType)
        {
            if (!entity.Attributes.TryGetValue(attributeName, out var raw) || raw == null)
            {
                return string.Empty;
            }

            if (raw is string logicalName)
            {
                return logicalName;
            }

            if (raw is int objectTypeCode && tableNamesByObjectType.TryGetValue(objectTypeCode, out var mapped))
            {
                return mapped;
            }

            if (raw is OptionSetValue option && tableNamesByObjectType.TryGetValue(option.Value, out mapped))
            {
                return mapped;
            }

            return entity.FormattedValues.TryGetValue(attributeName, out var formatted)
                ? formatted
                : string.Empty;
        }

        private static string EntityValue(Entity entity, string attributeName)
        {
            if (!entity.Attributes.TryGetValue(attributeName, out var value) || value == null)
            {
                return string.Empty;
            }

            return FormatValue(value);
        }

        private static Guid? EntityGuid(Entity entity, string attributeName)
        {
            return entity.Attributes.TryGetValue(attributeName, out var value) && value is Guid guid
                ? guid
                : (Guid?)null;
        }

        internal static string NormalizeDefinition(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return string.Empty;
            try
            {
                var document = XDocument.Parse(xml, LoadOptions.None);
                return CanonicalElement(document.Root!).ToString(SaveOptions.DisableFormatting);
            }
            catch
            {
                return xml.Trim().Replace("\r\n", "\n").Replace('\r', '\n');
            }
        }

        internal static string DefinitionFingerprint(string xml)
        {
            var normalized = NormalizeDefinition(xml);
            if (normalized.Length == 0) return string.Empty;

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                return "SHA-256 " + string.Concat(bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        private static XElement CanonicalElement(XElement element)
        {
            var normalized = new XElement(element.Name);
            foreach (var attribute in element.Attributes()
                .OrderBy(attribute => attribute.IsNamespaceDeclaration ? 0 : 1)
                .ThenBy(attribute => attribute.Name.ToString(), StringComparer.Ordinal))
            {
                normalized.Add(new XAttribute(attribute.Name, attribute.Value));
            }

            foreach (var node in element.Nodes())
            {
                if (node is XElement child)
                {
                    normalized.Add(CanonicalElement(child));
                }
                else if (node is XCData cdata)
                {
                    normalized.Add(new XCData(cdata.Value));
                }
                else if (node is XText text && !string.IsNullOrWhiteSpace(text.Value))
                {
                    normalized.Add(new XText(text.Value.Trim()));
                }
            }

            return normalized;
        }

        private static string ClassifyTable(EntityMetadata metadata)
        {
            if (metadata.IsBPFEntity == true)
            {
                return "BPF";
            }

            return metadata.IsIntersect == true ? "Intersect" : "Standard";
        }

        private static KeyValuePair<string, string> Property(string label, string propertyName) =>
            new KeyValuePair<string, string>(label, propertyName);
    }
}
