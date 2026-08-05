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
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EnvironmentComparison.Services
{
    public sealed class DataverseMetadataService
    {
        private const int PublishedPageSize = 5000;
        private const int UnpublishedDefinitionPageSize = 250;
        private const int ReportDefinitionPageSize = 25;
        private const int ProcessDefinitionPageSize = 100;
        private const int WorkflowCategory = 0;
        private const int BusinessRuleCategory = 2;
        private const int CloudFlowCategory = 5;

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
                forms.AddRange(LoadForms(
                    service,
                    tableNamesByObjectType,
                    retrieveAsIfPublished,
                    page => reportProgress?.Invoke(
                        60,
                        $"Loading {(retrieveAsIfPublished ? "unpublished " : string.Empty)}system forms page {page:N0}...")));
            }

            var views = new List<ViewMetadataInfo>();
            if ((areas & ComparisonAreas.Views) != 0)
            {
                reportProgress?.Invoke(80, "Loading system views...");
                views.AddRange(LoadViews(
                    service,
                    tableNamesByObjectType,
                    retrieveAsIfPublished,
                    page => reportProgress?.Invoke(
                        80,
                        $"Loading {(retrieveAsIfPublished ? "unpublished " : string.Empty)}system views page {page:N0}...")));
            }

            var processes = new List<ProcessMetadataInfo>();
            if ((areas & (ComparisonAreas.CloudFlows | ComparisonAreas.BusinessRules | ComparisonAreas.Workflows)) != 0)
            {
                reportProgress?.Invoke(85, "Loading process definitions...");
                processes.AddRange(LoadProcesses(
                    service,
                    areas,
                    retrieveAsIfPublished,
                    page => reportProgress?.Invoke(
                        85,
                        $"Loading {(retrieveAsIfPublished ? "unpublished " : string.Empty)}process definitions page {page:N0}...")));
            }

            var reports = new List<ReportMetadataInfo>();
            if ((areas & ComparisonAreas.Reports) != 0)
            {
                reportProgress?.Invoke(90, "Loading SSRS reports...");
                reports.AddRange(LoadReports(
                    service,
                    tableNamesByObjectType,
                    retrieveAsIfPublished,
                    page => reportProgress?.Invoke(
                        90,
                        $"Loading {(retrieveAsIfPublished ? "unpublished " : string.Empty)}SSRS report definitions page {page:N0}...")));
            }

            reportProgress?.Invoke(100, "Metadata snapshot loaded.");
            return new EnvironmentMetadataSnapshot(tables, forms, views, areas, retrieveAsIfPublished, reports, processes);
        }

        private static TableMetadataInfo ToTable(EntityMetadata metadata, bool includeColumns)
        {
            var properties = ReadProperties(metadata, TableProperties);
            properties["Metadata ID"] = metadata.MetadataId?.ToString("D") ?? string.Empty;
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
            properties["Metadata ID"] = metadata.MetadataId?.ToString("D") ?? string.Empty;
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
            IReadOnlyDictionary<int, string> tableNamesByObjectType,
            bool includeUnpublished,
            Action<int>? pageLoaded)
        {
            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet(
                    "formid",
                    // Retrieved only for the temporary raw diagnostic export. It is never used as a comparison key.
                    "formidunique",
                    "name",
                    "description",
                    "type",
                    "formxml",
                    "objecttypecode",
                    "formactivationstate",
                    "formpresentation",
                    "uniquename",
                    "componentstate",
                    "solutionid",
                    "ismanaged",
                    "publishedon",
                    "introducedversion",
                    "ancestorformid")
            };
            query.Criteria.AddCondition("componentstate", ConditionOperator.In, 0, 1);
            query.AddOrder("formid", OrderType.Ascending);

            var entities = CanonicalFormEntities(
                RetrieveAll(service, query, includeUnpublished, pageLoaded),
                includeUnpublished);
            var roleIds = entities
                .SelectMany(entity => FormRoleIds(EntityRawValue(entity, "formxml")))
                .Distinct()
                .ToList();
            var roles = LoadFormRoles(service, roleIds);

            foreach (var entity in entities)
            {
                var table = ResolveTableName(entity, "objecttypecode", tableNamesByObjectType);
                if (string.IsNullOrWhiteSpace(table))
                {
                    continue;
                }

                var name = EntityValue(entity, "name");
                var uniqueName = EntityValue(entity, "uniquename");
                var formId = EntityGuid(entity, "formid") ?? entity.Id;
                var rawFormXml = EntityRawValue(entity, "formxml");
                var assignedRoleIds = FormRoleIds(rawFormXml).Distinct().ToList();
                var key = !string.IsNullOrWhiteSpace(uniqueName)
                    ? $"{table}|unique:{uniqueName.Trim().ToLowerInvariant()}"
                    : $"{table}|id:{formId:D}";
                var properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Form ID"] = formId.ToString("D"),
                    ["Form ID unique"] = EntityValue(entity, "formidunique"),
                    ["Unique name"] = uniqueName,
                    ["Object type code"] = EntityValue(entity, "objecttypecode"),
                    ["Name"] = name,
                    ["Description"] = EntityValue(entity, "description"),
                    ["Form type"] = EntityValue(entity, "type"),
                    ["Activation state"] = EntityValue(entity, "formactivationstate"),
                    ["Presentation"] = EntityValue(entity, "formpresentation"),
                    ["Component state"] = EntityValue(entity, "componentstate"),
                    ["Solution ID"] = EntityValue(entity, "solutionid"),
                    ["Managed"] = EntityValue(entity, "ismanaged"),
                    ["Published on"] = EntityValue(entity, "publishedon"),
                    ["Introduced version"] = EntityValue(entity, "introducedversion"),
                    ["Ancestor form ID"] = EntityValue(entity, "ancestorformid"),
                    ["Form security roles"] = FormatFormSecurityRoles(assignedRoleIds, roles),
                    ["Form security role identities"] = FormatFormSecurityRoleIdentities(assignedRoleIds, roles),
                    ["Raw form security role IDs"] = string.Join(" | ", assignedRoleIds.OrderBy(id => id).Select(id => id.ToString("D"))),
                    ["Form XML"] = NormalizeFormDefinition(rawFormXml),
                    ["Raw Form XML"] = rawFormXml
                };
                yield return new FormMetadataInfo(key, table, name, properties);
            }
        }

        private static IReadOnlyList<Entity> CanonicalFormEntities(
            IEnumerable<Entity> entities,
            bool includeUnpublished)
        {
            return entities
                .GroupBy(entity => EntityGuid(entity, "formid") ?? entity.Id)
                .Select(group => group
                    .OrderByDescending(entity => FormRecordPriority(entity, includeUnpublished))
                    .ThenBy(entity => entity.Id)
                    .First())
                .ToList();
        }

        private static int FormRecordPriority(Entity entity, bool includeUnpublished)
        {
            var componentState = EntityValue(entity, "componentstate");
            if (includeUnpublished && componentState == "1") return 3;
            if (componentState == "0") return 2;
            if (!includeUnpublished && componentState == "1") return 1;
            return 0;
        }

        private static IReadOnlyDictionary<Guid, FormRoleInfo> LoadFormRoles(
            IOrganizationService service,
            IReadOnlyList<Guid> roleIds)
        {
            var result = new Dictionary<Guid, FormRoleInfo>();
            const int batchSize = 500;
            for (var offset = 0; offset < roleIds.Count; offset += batchSize)
            {
                var batch = roleIds.Skip(offset).Take(batchSize).Cast<object>().ToArray();
                var query = new QueryExpression("role")
                {
                    ColumnSet = new ColumnSet("roleid", "name", "roletemplateid", "parentrootroleid")
                };
                query.Criteria.AddCondition("roleid", ConditionOperator.In, batch);
                foreach (var role in RetrieveAll(service, query))
                {
                    var templateId = EntityReferenceId(role, "roletemplateid");
                    var rootRoleId = EntityReferenceId(role, "parentrootroleid");
                    var identity = templateId.HasValue
                        ? $"template:{templateId.Value:D}"
                        : rootRoleId.HasValue
                            ? $"root:{rootRoleId.Value:D}"
                            : $"role:{role.Id:D}";
                    result[role.Id] = new FormRoleInfo(identity, EntityValue(role, "name"));
                }
            }

            return result;
        }

        private static IReadOnlyList<Guid> FormRoleIds(string formXml)
        {
            if (string.IsNullOrWhiteSpace(formXml)) return Array.Empty<Guid>();
            try
            {
                var document = XDocument.Parse(formXml, LoadOptions.None);
                return document
                    .Descendants()
                    .Where(element => element.Name.LocalName.Equals("DisplayConditions", StringComparison.OrdinalIgnoreCase))
                    .SelectMany(element => element.Elements())
                    .Where(element => element.Name.LocalName.Equals("Role", StringComparison.OrdinalIgnoreCase))
                    .Select(element => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals("Id", StringComparison.OrdinalIgnoreCase))?.Value)
                    .Where(value => Guid.TryParse(value, out _))
                    .Select(value => Guid.Parse(value!))
                    .ToList();
            }
            catch
            {
                return Array.Empty<Guid>();
            }
        }

        private static string FormatFormSecurityRoleIdentities(
            IEnumerable<Guid> roleIds,
            IReadOnlyDictionary<Guid, FormRoleInfo> roles)
        {
            return string.Join(" | ", roleIds
                .Select(roleId => roles.TryGetValue(roleId, out var role) ? role.Identity : $"role:{roleId:D}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(identity => identity, StringComparer.OrdinalIgnoreCase));
        }

        private static string FormatFormSecurityRoles(
            IEnumerable<Guid> roleIds,
            IReadOnlyDictionary<Guid, FormRoleInfo> roles)
        {
            return string.Join(" | ", roleIds
                .Select(roleId => roles.TryGetValue(roleId, out var role)
                    ? new { role.Identity, Name = string.IsNullOrWhiteSpace(role.Name) ? "Unnamed role" : role.Name }
                    : new { Identity = $"role:{roleId:D}", Name = "Unresolved role" })
                .GroupBy(role => role.Identity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(role => role.Identity, StringComparer.OrdinalIgnoreCase)
                .Select(role => $"{role.Name} [{role.Identity}]"));
        }

        private static IEnumerable<ViewMetadataInfo> LoadViews(
            IOrganizationService service,
            IReadOnlyDictionary<int, string> tableNamesByObjectType,
            bool includeUnpublished,
            Action<int>? pageLoaded)
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

            foreach (var entity in RetrieveAll(service, query, includeUnpublished, pageLoaded))
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
                    ["View ID"] = entity.Id.ToString("D"),
                    ["Returned type code"] = EntityValue(entity, "returnedtypecode"),
                    ["Name"] = name,
                    ["Query type"] = EntityValue(entity, "querytype"),
                    ["Default view"] = EntityValue(entity, "isdefault"),
                    ["Quick find view"] = EntityValue(entity, "isquickfindquery"),
                    ["Fetch XML"] = NormalizeDefinition(EntityRawValue(entity, "fetchxml")),
                    ["Layout XML"] = NormalizeDefinition(EntityRawValue(entity, "layoutxml")),
                    ["Column set XML"] = NormalizeDefinition(EntityRawValue(entity, "columnsetxml")),
                    ["Raw Fetch XML"] = EntityRawValue(entity, "fetchxml"),
                    ["Raw Layout XML"] = EntityRawValue(entity, "layoutxml"),
                    ["Raw Column set XML"] = EntityRawValue(entity, "columnsetxml"),
                    ["Advanced group by"] = EntityValue(entity, "advancedgroupby")
                };
                yield return new ViewMetadataInfo(key, table, name, properties);
            }
        }

        private static IEnumerable<ProcessMetadataInfo> LoadProcesses(
            IOrganizationService service,
            ComparisonAreas areas,
            bool includeUnpublished,
            Action<int>? pageLoaded)
        {
            var categories = SelectedProcessCategories(areas);
            if (categories.Count == 0)
            {
                yield break;
            }

            var query = new QueryExpression("workflow")
            {
                ColumnSet = new ColumnSet(
                    "workflowid",
                    "workflowidunique",
                    "uniquename",
                    "name",
                    "description",
                    "category",
                    "type",
                    "primaryentity",
                    "statecode",
                    "statuscode",
                    "mode",
                    "scope",
                    "ondemand",
                    "subprocess",
                    "runas",
                    "languagecode",
                    "triggeroncreate",
                    "triggerondelete",
                    "triggeronupdateattributelist",
                    "createstage",
                    "updatestage",
                    "deletestage",
                    "rank",
                    "processorder",
                    "syncworkflowlogonfailure",
                    "asyncautodelete",
                    "modernflowtype",
                    "businessprocesstype",
                    "processtriggerscope",
                    "processtriggerformid",
                    "definition",
                    "clientdata",
                    "xaml",
                    "connectionreferences",
                    "inputs",
                    "outputs",
                    "metadata",
                    "componentstate",
                    "solutionid",
                    "ismanaged",
                    "introducedversion",
                    "activeworkflowid",
                    "parentworkflowid")
            };
            query.Criteria.AddCondition("category", ConditionOperator.In, categories.Cast<object>().ToArray());
            query.Criteria.AddCondition("type", ConditionOperator.Equal, 1);
            query.Criteria.AddCondition("componentstate", ConditionOperator.In, 0, 1);
            query.AddOrder("workflowid", OrderType.Ascending);

            var entities = CanonicalProcessEntities(
                RetrieveAll(service, query, includeUnpublished, pageLoaded, ProcessDefinitionPageSize),
                includeUnpublished);
            foreach (var entity in entities)
            {
                var category = EntityOptionValue(entity, "category");
                var scope = ProcessScope(category);
                if (!scope.HasValue)
                {
                    continue;
                }

                var processId = EntityGuid(entity, "workflowid") ?? entity.Id;
                var name = EntityValue(entity, "name");
                var primaryEntity = EntityValue(entity, "primaryentity");
                var rawDefinition = EntityRawValue(entity, "definition");
                var rawClientData = EntityRawValue(entity, "clientdata");
                var rawXaml = EntityRawValue(entity, "xaml");
                var rawConnectionReferences = EntityRawValue(entity, "connectionreferences");
                var rawInputs = EntityRawValue(entity, "inputs");
                var rawOutputs = EntityRawValue(entity, "outputs");
                var rawMetadata = EntityRawValue(entity, "metadata");
                var properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Process ID"] = processId.ToString("D"),
                    ["Process ID unique"] = EntityValue(entity, "workflowidunique"),
                    ["Unique name"] = EntityValue(entity, "uniquename"),
                    ["Name"] = name,
                    ["Description"] = EntityValue(entity, "description"),
                    ["Category"] = EntityValue(entity, "category"),
                    ["Type"] = EntityValue(entity, "type"),
                    ["Primary entity"] = primaryEntity,
                    ["State"] = EntityValue(entity, "statecode"),
                    ["Status"] = EntityValue(entity, "statuscode"),
                    ["Mode"] = EntityValue(entity, "mode"),
                    ["Scope"] = EntityValue(entity, "scope"),
                    ["On demand"] = EntityValue(entity, "ondemand"),
                    ["Subprocess"] = EntityValue(entity, "subprocess"),
                    ["Run as"] = EntityValue(entity, "runas"),
                    ["Language code"] = EntityValue(entity, "languagecode"),
                    ["Trigger on create"] = EntityValue(entity, "triggeroncreate"),
                    ["Trigger on delete"] = EntityValue(entity, "triggerondelete"),
                    ["Trigger on update columns"] = NormalizeDelimitedList(EntityRawValue(entity, "triggeronupdateattributelist")),
                    ["Create stage"] = EntityValue(entity, "createstage"),
                    ["Update stage"] = EntityValue(entity, "updatestage"),
                    ["Delete stage"] = EntityValue(entity, "deletestage"),
                    ["Rank"] = EntityValue(entity, "rank"),
                    ["Process order"] = EntityValue(entity, "processorder"),
                    ["Log workflow errors"] = EntityValue(entity, "syncworkflowlogonfailure"),
                    ["Delete completed jobs"] = EntityValue(entity, "asyncautodelete"),
                    ["Modern flow type"] = EntityValue(entity, "modernflowtype"),
                    ["Business process type"] = EntityValue(entity, "businessprocesstype"),
                    ["Process trigger scope"] = EntityValue(entity, "processtriggerscope"),
                    ["Process trigger form ID"] = EntityValue(entity, "processtriggerformid"),
                    ["Definition"] = NormalizeStructuredDefinition(rawDefinition),
                    ["Client data"] = NormalizeStructuredDefinition(rawClientData),
                    ["XAML"] = NormalizeStructuredDefinition(rawXaml),
                    ["Connection references"] = NormalizeStructuredDefinition(rawConnectionReferences),
                    ["Inputs"] = NormalizeStructuredDefinition(rawInputs),
                    ["Outputs"] = NormalizeStructuredDefinition(rawOutputs),
                    ["Metadata"] = NormalizeStructuredDefinition(rawMetadata),
                    ["Component state"] = EntityValue(entity, "componentstate"),
                    ["Solution ID"] = EntityValue(entity, "solutionid"),
                    ["Managed"] = EntityValue(entity, "ismanaged"),
                    ["Introduced version"] = EntityValue(entity, "introducedversion"),
                    ["Active process ID"] = EntityValue(entity, "activeworkflowid"),
                    ["Parent process ID"] = EntityValue(entity, "parentworkflowid"),
                    ["Raw Definition"] = rawDefinition,
                    ["Raw Client data"] = rawClientData,
                    ["Raw XAML"] = rawXaml,
                    ["Raw Connection references"] = rawConnectionReferences,
                    ["Raw Inputs"] = rawInputs,
                    ["Raw Outputs"] = rawOutputs,
                    ["Raw Metadata"] = rawMetadata
                };
                yield return new ProcessMetadataInfo(
                    $"{scope.Value}|id:{processId:D}",
                    scope.Value,
                    NormalizeProcessTableLogicalName(primaryEntity),
                    name,
                    properties);
            }
        }

        private static IReadOnlyList<Entity> CanonicalProcessEntities(
            IEnumerable<Entity> entities,
            bool includeUnpublished)
        {
            return entities
                .GroupBy(entity => EntityGuid(entity, "workflowid") ?? entity.Id)
                .Select(group => group
                    .OrderByDescending(entity => ProcessRecordPriority(entity, includeUnpublished))
                    .ThenBy(entity => entity.Id)
                    .First())
                .ToList();
        }

        private static int ProcessRecordPriority(Entity entity, bool includeUnpublished)
        {
            var componentState = EntityValue(entity, "componentstate");
            if (includeUnpublished && componentState == "1") return 3;
            if (componentState == "0") return 2;
            if (!includeUnpublished && componentState == "1") return 1;
            return 0;
        }

        private static IReadOnlyList<int> SelectedProcessCategories(ComparisonAreas areas)
        {
            var categories = new List<int>();
            if ((areas & ComparisonAreas.Workflows) != 0) categories.Add(WorkflowCategory);
            if ((areas & ComparisonAreas.BusinessRules) != 0) categories.Add(BusinessRuleCategory);
            if ((areas & ComparisonAreas.CloudFlows) != 0) categories.Add(CloudFlowCategory);
            return categories;
        }

        private static ComparisonScope? ProcessScope(int? category)
        {
            switch (category)
            {
                case WorkflowCategory:
                    return ComparisonScope.Workflow;
                case BusinessRuleCategory:
                    return ComparisonScope.BusinessRule;
                case CloudFlowCategory:
                    return ComparisonScope.CloudFlow;
                default:
                    return null;
            }
        }

        private static string NormalizeProcessTableLogicalName(string primaryEntity)
        {
            return string.Equals(primaryEntity?.Trim(), "none", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : primaryEntity?.Trim() ?? string.Empty;
        }

        private static IEnumerable<ReportMetadataInfo> LoadReports(
            IOrganizationService service,
            IReadOnlyDictionary<int, string> tableNamesByObjectType,
            bool includeUnpublished,
            Action<int>? pageLoaded)
        {
            var query = new QueryExpression("report")
            {
                ColumnSet = new ColumnSet(
                    "reportid",
                    "reportidunique",
                    "name",
                    "description",
                    "filename",
                    "reporttypecode",
                    "reportstatus",
                    "languagecode",
                    "mimetype",
                    "defaultfilter",
                    "bodytext",
                    "ispersonal",
                    "componentstate",
                    "solutionid",
                    "ismanaged",
                    "introducedversion",
                    "reportversion",
                    "iscustomreport",
                    "isscheduledreport",
                    "parentreportid",
                    "dependentmodelreportid")
            };
            query.Criteria.AddCondition("ispersonal", ConditionOperator.Equal, false);
            query.Criteria.AddCondition("reporttypecode", ConditionOperator.Equal, 1);

            var entities = RetrieveAll(
                service,
                query,
                includeUnpublished,
                pageLoaded,
                ReportDefinitionPageSize);
            var reportIds = entities
                .Select(entity => EntityGuid(entity, "reportid") ?? entity.Id)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
            var associatedTables = LoadReportRelatedValues(
                service,
                "reportentity",
                "objecttypecode",
                reportIds,
                includeUnpublished,
                entity => ResolveTableName(entity, "objecttypecode", tableNamesByObjectType));
            var categories = LoadReportRelatedValues(
                service,
                "reportcategory",
                "categorycode",
                reportIds,
                includeUnpublished,
                entity => EntityValue(entity, "categorycode"));
            var visibility = LoadReportRelatedValues(
                service,
                "reportvisibility",
                "visibilitycode",
                reportIds,
                includeUnpublished,
                entity => EntityValue(entity, "visibilitycode"));

            foreach (var entity in entities)
            {
                var reportId = EntityGuid(entity, "reportid") ?? entity.Id;
                var name = EntityValue(entity, "name");
                var rawRdl = EntityRawValue(entity, "bodytext");
                var rawDefaultFilter = EntityRawValue(entity, "defaultfilter");
                var properties = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Report ID"] = reportId.ToString("D"),
                    ["Report ID unique"] = EntityValue(entity, "reportidunique"),
                    ["Name"] = name,
                    ["Description"] = EntityValue(entity, "description"),
                    ["File name"] = EntityValue(entity, "filename"),
                    ["Report type"] = EntityValue(entity, "reporttypecode"),
                    ["Report status"] = EntityValue(entity, "reportstatus"),
                    ["Language code"] = EntityValue(entity, "languagecode"),
                    ["MIME type"] = EntityValue(entity, "mimetype"),
                    ["Default filter"] = NormalizeDefinition(rawDefaultFilter),
                    ["RDL"] = NormalizeDefinition(rawRdl),
                    ["Associated tables"] = RelatedReportValues(associatedTables, reportId),
                    ["Categories"] = RelatedReportValues(categories, reportId),
                    ["Visibility"] = RelatedReportValues(visibility, reportId),
                    ["Personal"] = EntityValue(entity, "ispersonal"),
                    ["Component state"] = EntityValue(entity, "componentstate"),
                    ["Solution ID"] = EntityValue(entity, "solutionid"),
                    ["Managed"] = EntityValue(entity, "ismanaged"),
                    ["Introduced version"] = EntityValue(entity, "introducedversion"),
                    ["Report version"] = EntityValue(entity, "reportversion"),
                    ["Custom report"] = EntityValue(entity, "iscustomreport"),
                    ["Scheduled report"] = EntityValue(entity, "isscheduledreport"),
                    ["Parent report ID"] = EntityValue(entity, "parentreportid"),
                    ["Dependent model report ID"] = EntityValue(entity, "dependentmodelreportid"),
                    ["Raw default filter"] = rawDefaultFilter,
                    ["Raw RDL"] = rawRdl
                };
                yield return new ReportMetadataInfo($"report|id:{reportId:D}", name, properties);
            }
        }

        private static IReadOnlyDictionary<Guid, IReadOnlyList<string>> LoadReportRelatedValues(
            IOrganizationService service,
            string entityName,
            string valueAttribute,
            IReadOnlyList<Guid> reportIds,
            bool includeUnpublished,
            Func<Entity, string> readValue)
        {
            var values = new Dictionary<Guid, List<string>>();
            const int batchSize = 500;
            for (var offset = 0; offset < reportIds.Count; offset += batchSize)
            {
                var batch = reportIds.Skip(offset).Take(batchSize).Cast<object>().ToArray();
                var query = new QueryExpression(entityName)
                {
                    ColumnSet = new ColumnSet("reportid", valueAttribute)
                };
                query.Criteria.AddCondition("reportid", ConditionOperator.In, batch);
                foreach (var entity in RetrieveAll(service, query, includeUnpublished, null))
                {
                    var reportId = EntityReferenceId(entity, "reportid") ?? EntityGuid(entity, "reportid");
                    var value = readValue(entity);
                    if (!reportId.HasValue || string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    if (!values.TryGetValue(reportId.Value, out var reportValues))
                    {
                        reportValues = new List<string>();
                        values[reportId.Value] = reportValues;
                    }

                    reportValues.Add(value);
                }
            }

            return values.ToDictionary(
                item => item.Key,
                item => (IReadOnlyList<string>)item.Value
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList());
        }

        private static string RelatedReportValues(
            IReadOnlyDictionary<Guid, IReadOnlyList<string>> values,
            Guid reportId)
        {
            return values.TryGetValue(reportId, out var reportValues)
                ? string.Join(" | ", reportValues)
                : string.Empty;
        }

        private static List<Entity> RetrieveAll(IOrganizationService service, QueryExpression query)
        {
            return RetrieveAll(service, query, false, null);
        }

        private static List<Entity> RetrieveAll(
            IOrganizationService service,
            QueryExpression query,
            bool includeUnpublished,
            Action<int>? pageLoaded,
            int? pageSize = null)
        {
            var result = new List<Entity>();
            query.PageInfo = new PagingInfo
            {
                Count = pageSize ?? (includeUnpublished ? UnpublishedDefinitionPageSize : PublishedPageSize),
                PageNumber = 1
            };
            while (true)
            {
                var page = includeUnpublished
                    ? ((RetrieveUnpublishedMultipleResponse)service.Execute(
                        new RetrieveUnpublishedMultipleRequest { Query = query })).EntityCollection
                    : service.RetrieveMultiple(query);
                result.AddRange(page.Entities);
                pageLoaded?.Invoke(query.PageInfo.PageNumber);
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

        private static string EntityRawValue(Entity entity, string attributeName)
        {
            if (!entity.Attributes.TryGetValue(attributeName, out var value) || value == null)
            {
                return string.Empty;
            }

            return value is string text ? text : FormatValue(value);
        }

        private static Guid? EntityGuid(Entity entity, string attributeName)
        {
            return entity.Attributes.TryGetValue(attributeName, out var value) && value is Guid guid
                ? guid
                : (Guid?)null;
        }

        private static int? EntityOptionValue(Entity entity, string attributeName)
        {
            if (!entity.Attributes.TryGetValue(attributeName, out var value) || value == null)
            {
                return null;
            }

            if (value is OptionSetValue option) return option.Value;
            return value is int integer ? integer : (int?)null;
        }

        private static Guid? EntityReferenceId(Entity entity, string attributeName)
        {
            if (!entity.Attributes.TryGetValue(attributeName, out var value) || value == null)
            {
                return null;
            }

            if (value is EntityReference reference) return reference.Id;
            return value is Guid guid ? guid : (Guid?)null;
        }

        internal static string NormalizeFormDefinition(string xml)
        {
            var normalized = NormalizeDefinition(xml);
            if (normalized.Length == 0) return string.Empty;
            try
            {
                var document = XDocument.Parse(normalized, LoadOptions.None);
                foreach (var element in document.Root?.DescendantsAndSelf() ?? Enumerable.Empty<XElement>())
                {
                    element.Attributes()
                        .Where(attribute => attribute.Name.LocalName.Equals("labelid", StringComparison.OrdinalIgnoreCase))
                        .Remove();
                }

                foreach (var cell in document
                    .Descendants()
                    .Where(element => element.Name.LocalName.Equals("cell", StringComparison.OrdinalIgnoreCase))
                    .Where(IsEmptyPlaceholderCell))
                {
                    cell.Attributes()
                        .Where(attribute => attribute.Name.LocalName.Equals("id", StringComparison.OrdinalIgnoreCase))
                        .Remove();
                }

                document
                    .Descendants()
                    .Where(element => element.Name.LocalName.Equals("DisplayConditions", StringComparison.OrdinalIgnoreCase))
                    .SelectMany(element => element.Elements())
                    .Where(element => element.Name.LocalName.Equals("Role", StringComparison.OrdinalIgnoreCase))
                    .Remove();
                return document.Root?.ToString(SaveOptions.DisableFormatting) ?? normalized;
            }
            catch
            {
                return normalized;
            }
        }

        private static bool IsEmptyPlaceholderCell(XElement cell)
        {
            if (cell.Descendants().Any(IsFunctionalFormElement))
            {
                return false;
            }

            return !cell
                .Descendants()
                .Where(element => element.Name.LocalName.Equals("label", StringComparison.OrdinalIgnoreCase))
                .SelectMany(element => element.Attributes())
                .Where(attribute => attribute.Name.LocalName.Equals("description", StringComparison.OrdinalIgnoreCase))
                .Any(attribute => !string.IsNullOrWhiteSpace(attribute.Value));
        }

        private static bool IsFunctionalFormElement(XElement element)
        {
            var name = element.Name.LocalName;
            return name.Equals("control", StringComparison.OrdinalIgnoreCase)
                || name.Equals("data", StringComparison.OrdinalIgnoreCase)
                || name.Equals("event", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Handler", StringComparison.OrdinalIgnoreCase);
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

        internal static string NormalizeStructuredDefinition(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var trimmed = value.Trim();
            try
            {
                var document = XDocument.Parse(trimmed, LoadOptions.None);
                return CanonicalElement(document.Root!).ToString(SaveOptions.DisableFormatting);
            }
            catch
            {
                // Process definitions may be JSON rather than XML.
            }

            try
            {
                return CanonicalJson(JToken.Parse(trimmed)).ToString(Formatting.None);
            }
            catch
            {
                return trimmed.Replace("\r\n", "\n").Replace('\r', '\n');
            }
        }

        private static JToken CanonicalJson(JToken token)
        {
            if (token is JObject jsonObject)
            {
                return new JObject(jsonObject.Properties()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => new JProperty(property.Name, CanonicalJson(property.Value))));
            }

            if (token is JArray jsonArray)
            {
                return new JArray(jsonArray.Select(CanonicalJson));
            }

            return token.DeepClone();
        }

        private static string NormalizeDelimitedList(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return string.Join(",", value
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase));
        }

        internal static string DefinitionFingerprint(string xml)
        {
            var normalized = NormalizeStructuredDefinition(xml);
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

        private sealed class FormRoleInfo
        {
            public FormRoleInfo(string identity, string name)
            {
                Identity = identity;
                Name = name;
            }

            public string Identity { get; }

            public string Name { get; }
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
