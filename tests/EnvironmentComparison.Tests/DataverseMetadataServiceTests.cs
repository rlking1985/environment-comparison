using System;
using System.Collections.Generic;
using System.Linq;
using EnvironmentComparison.Domain;
using EnvironmentComparison.Services;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace EnvironmentComparison.Tests
{
    [TestClass]
    public sealed class DataverseMetadataServiceTests
    {
        [TestMethod]
        public void LoadSnapshotUsesOnlyReadOperationsAndSelectedAreas()
        {
            var service = new RecordingService();

            var snapshot = new DataverseMetadataService().LoadSnapshot(
                service,
                ComparisonAreas.TableMetadata | ComparisonAreas.Forms | ComparisonAreas.Views,
                false);

            Assert.AreEqual(0, snapshot.Tables.Count);
            Assert.AreEqual(1, service.ExecuteRequests.Count);
            var request = service.ExecuteRequests[0] as RetrieveAllEntitiesRequest;
            Assert.IsNotNull(request);
            Assert.AreEqual(EntityFilters.Entity, request.EntityFilters);
            Assert.IsFalse(request.RetrieveAsIfPublished);
            CollectionAssert.AreEquivalent(new[] { "systemform", "savedquery" }, service.RetrievedEntityNames.ToArray());
            Assert.AreEqual(0, service.WriteAttempts);
        }

        [TestMethod]
        public void ColumnSelectionRequestsAttributeMetadata()
        {
            var service = new RecordingService();

            new DataverseMetadataService().LoadSnapshot(service, ComparisonAreas.Columns, true);

            var request = (RetrieveAllEntitiesRequest)service.ExecuteRequests[0];
            Assert.AreEqual(EntityFilters.Entity | EntityFilters.Attributes, request.EntityFilters);
            Assert.IsTrue(request.RetrieveAsIfPublished);
            Assert.AreEqual(0, service.RetrievedEntityNames.Count);
        }

        [TestMethod]
        public void IncludeUnpublishedUsesTheReadOnlyUnpublishedRequestForFormsAndViews()
        {
            var formId = Guid.Parse("12345678-1234-1234-1234-1234567890ab");
            var viewId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var service = new RecordingService();
            service.UnpublishedRetrieveResults["systemform"] = Forms(
                formId,
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
            var view = new Entity("savedquery", viewId);
            view["returnedtypecode"] = "account";
            view["name"] = "Active Accounts";
            service.UnpublishedRetrieveResults["savedquery"] = new EntityCollection(new List<Entity> { view });
            var progress = new List<string>();

            var snapshot = new DataverseMetadataService().LoadSnapshot(
                service,
                ComparisonAreas.Forms | ComparisonAreas.Views,
                true,
                (_, message) => progress.Add(message));

            Assert.IsTrue(snapshot.IncludesUnpublishedMetadata);
            Assert.AreEqual(1, snapshot.Forms.Count);
            Assert.AreEqual(1, snapshot.Views.Count);
            CollectionAssert.AreEquivalent(
                new[] { "systemform", "savedquery" },
                service.UnpublishedRetrievedEntityNames.ToArray());
            CollectionAssert.AreEquivalent(
                new[] { 250, 250 },
                service.RetrievedQueries.Select(query => query.PageInfo.Count).ToArray());
            Assert.IsTrue(progress.Any(message => message.Contains("unpublished system forms page 1")));
            Assert.IsTrue(progress.Any(message => message.Contains("unpublished system views page 1")));
            Assert.AreEqual(0, service.RetrievedEntityNames.Count);
            Assert.AreEqual(0, service.WriteAttempts);
        }

        [TestMethod]
        public void FormFallbackUsesFormIdRatherThanFormIdUnique()
        {
            var formId = Guid.Parse("12345678-1234-1234-1234-1234567890ab");
            var formIdUniqueA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var formIdUniqueB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var environmentA = new RecordingService();
            var environmentB = new RecordingService();
            environmentA.RetrieveResults["systemform"] = Forms(formId, formIdUniqueA);
            environmentB.RetrieveResults["systemform"] = Forms(formId, formIdUniqueB);

            var snapshotA = new DataverseMetadataService().LoadSnapshot(
                environmentA,
                ComparisonAreas.Forms,
                false);
            var snapshotB = new DataverseMetadataService().LoadSnapshot(
                environmentB,
                ComparisonAreas.Forms,
                false);

            Assert.AreEqual(1, snapshotA.Forms.Count);
            Assert.AreEqual(1, snapshotB.Forms.Count);
            Assert.AreEqual($"account|id:{formId:D}", snapshotA.Forms[0].Key);
            Assert.AreEqual(snapshotA.Forms[0].Key, snapshotB.Forms[0].Key);
            Assert.AreEqual(formIdUniqueA.ToString("D"), snapshotA.Forms[0].GetProperty("Form ID unique"));
            Assert.AreEqual(formIdUniqueB.ToString("D"), snapshotB.Forms[0].GetProperty("Form ID unique"));
            Assert.AreEqual("\r\n<form />\r\n", snapshotA.Forms[0].GetProperty("Raw Form XML"));
            CollectionAssert.Contains(
                environmentA.RetrievedQueries.Single().ColumnSet.Columns,
                "formidunique");
            CollectionAssert.Contains(
                environmentA.RetrievedQueries.Single().ColumnSet.Columns,
                "componentstate");
            Assert.AreEqual("formid", environmentA.RetrievedQueries.Single().Orders.Single().AttributeName);
            Assert.IsTrue(environmentA.RetrievedQueries.Single().Criteria.Conditions.Any(condition =>
                condition.AttributeName == "componentstate"
                && condition.Operator == ConditionOperator.In));
        }

        [TestMethod]
        public void DuplicateRetrievedFormIdsAreCollapsed()
        {
            var formId = Guid.Parse("12345678-1234-1234-1234-1234567890ab");
            var service = new RecordingService();
            var form = Forms(formId, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")).Entities.Single();
            service.RetrieveResults["systemform"] = new EntityCollection(new List<Entity> { form, form });

            var snapshot = new DataverseMetadataService().LoadSnapshot(service, ComparisonAreas.Forms, false);

            Assert.AreEqual(1, snapshot.Forms.Count);
            Assert.AreEqual(formId.ToString("D"), snapshot.Forms[0].GetProperty("Form ID"));
        }

        [TestMethod]
        public void SystemFormRolesUseStableRoleTemplateIdentityAndPreserveRawIds()
        {
            var formId = Guid.Parse("12345678-1234-1234-1234-1234567890ab");
            var roleIdA = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
            var roleIdB = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
            var templateId = Guid.Parse("627090ff-40a3-4053-8790-584edc5be201");
            var environmentA = new RecordingService();
            var environmentB = new RecordingService();
            environmentA.RetrieveResults["systemform"] = FormWithRole(formId, roleIdA);
            environmentB.RetrieveResults["systemform"] = FormWithRole(formId, roleIdB);
            environmentA.RetrieveResults["role"] = Roles(Role(roleIdA, templateId, "System Administrator"));
            environmentB.RetrieveResults["role"] = Roles(Role(roleIdB, templateId, "System Administrator"));

            var snapshotA = new DataverseMetadataService().LoadSnapshot(environmentA, ComparisonAreas.Forms, false);
            var snapshotB = new DataverseMetadataService().LoadSnapshot(environmentB, ComparisonAreas.Forms, false);
            var result = new MetadataComparisonService().Compare(snapshotA, snapshotB);

            Assert.AreEqual($"template:{templateId:D}", snapshotA.Forms[0].GetProperty("Form security role identities"));
            Assert.AreEqual(
                snapshotA.Forms[0].GetProperty("Form security role identities"),
                snapshotB.Forms[0].GetProperty("Form security role identities"));
            Assert.AreEqual(snapshotA.Forms[0].GetProperty("Form XML"), snapshotB.Forms[0].GetProperty("Form XML"));
            StringAssert.Contains(snapshotA.Forms[0].GetProperty("Raw Form XML"), roleIdA.ToString("D"));
            StringAssert.Contains(snapshotB.Forms[0].GetProperty("Raw Form XML"), roleIdB.ToString("D"));
            Assert.IsFalse(result.Issues.Any(issue => issue.PropertyName == "Form XML" || issue.PropertyName == "Form security roles"));
            CollectionAssert.Contains(environmentA.RetrievedEntityNames.ToArray(), "role");
            Assert.AreEqual(0, environmentA.WriteAttempts);
            Assert.AreEqual(0, environmentB.WriteAttempts);
        }

        [TestMethod]
        public void FormRolesWithoutTemplatesUseParentRootRoleIdentity()
        {
            var formId = Guid.Parse("12345678-1234-1234-1234-1234567890ab");
            var roleIdA = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
            var roleIdB = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
            var rootRoleId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
            var environmentA = new RecordingService();
            var environmentB = new RecordingService();
            environmentA.RetrieveResults["systemform"] = FormWithRole(formId, roleIdA);
            environmentB.RetrieveResults["systemform"] = FormWithRole(formId, roleIdB);
            environmentA.RetrieveResults["role"] = Roles(RoleWithoutTemplate(roleIdA, rootRoleId, "Student Services"));
            environmentB.RetrieveResults["role"] = Roles(RoleWithoutTemplate(roleIdB, rootRoleId, "Student Services"));

            var snapshotA = new DataverseMetadataService().LoadSnapshot(environmentA, ComparisonAreas.Forms, false);
            var snapshotB = new DataverseMetadataService().LoadSnapshot(environmentB, ComparisonAreas.Forms, false);

            Assert.AreEqual($"root:{rootRoleId:D}", snapshotA.Forms[0].GetProperty("Form security role identities"));
            Assert.AreEqual(
                snapshotA.Forms[0].GetProperty("Form security role identities"),
                snapshotB.Forms[0].GetProperty("Form security role identities"));
            Assert.AreEqual(0, new MetadataComparisonService().Compare(snapshotA, snapshotB).Issues.Count);
        }

        [TestMethod]
        public void MapsTableAndImportantColumnMetadataWithoutManagedState()
        {
            var tableMetadataId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var columnMetadataId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var entityMetadata = new EntityMetadata
            {
                MetadataId = tableMetadataId,
                LogicalName = "new_student",
                SchemaName = "new_Student",
                DisplayName = new Label("Student", 1033),
                OwnershipType = OwnershipTypes.UserOwned,
                IsAuditEnabled = new BooleanManagedProperty(true)
            };
            typeof(EntityMetadata)
                .GetProperty("Attributes")!
                .GetSetMethod(true)!
                .Invoke(entityMetadata, new object[]
                {
                    new AttributeMetadata[]
                    {
                        new StringAttributeMetadata
                        {
                            MetadataId = columnMetadataId,
                            LogicalName = "new_code",
                            SchemaName = "new_Code",
                            DisplayName = new Label("Student code", 1033),
                            MaxLength = 100,
                            RequiredLevel = new AttributeRequiredLevelManagedProperty(AttributeRequiredLevel.ApplicationRequired),
                            IsAuditEnabled = new BooleanManagedProperty(true)
                        }
                    }
                });
            var service = new RecordingService
            {
                EntityMetadata = new[] { entityMetadata }
            };

            var snapshot = new DataverseMetadataService().LoadSnapshot(
                service,
                ComparisonAreas.TableMetadata | ComparisonAreas.Columns,
                false);

            Assert.AreEqual(1, snapshot.Tables.Count);
            Assert.AreEqual("Student", snapshot.Tables[0].DisplayName);
            Assert.AreEqual(tableMetadataId.ToString("D"), snapshot.Tables[0].GetProperty("Metadata ID"));
            Assert.AreEqual("Standard", snapshot.Tables[0].Classification);
            Assert.AreEqual("UserOwned", snapshot.Tables[0].GetProperty("Ownership type"));
            Assert.AreEqual("True", snapshot.Tables[0].GetProperty("Audit enabled"));
            Assert.AreEqual(1, snapshot.Tables[0].Columns.Count);
            Assert.AreEqual(columnMetadataId.ToString("D"), snapshot.Tables[0].Columns[0].GetProperty("Metadata ID"));
            Assert.AreEqual("100", snapshot.Tables[0].Columns[0].GetProperty("Maximum length"));
            Assert.AreEqual("ApplicationRequired", snapshot.Tables[0].Columns[0].GetProperty("Requirement level"));
            Assert.IsFalse(snapshot.Tables[0].Properties.ContainsKey("Managed"));
            Assert.IsFalse(snapshot.Tables[0].Columns[0].Properties.ContainsKey("Managed"));
        }

        [TestMethod]
        public void ClassifiesStandardIntersectAndBpfTables()
        {
            var service = new RecordingService
            {
                EntityMetadata = new[]
                {
                    Entity("new_standard", false, false),
                    Entity("new_intersect", true, false),
                    Entity("new_bpf", false, true)
                }
            };

            var snapshot = new DataverseMetadataService().LoadSnapshot(
                service,
                ComparisonAreas.TableMetadata,
                false);
            var classifications = snapshot.Tables.ToDictionary(table => table.LogicalName, table => table.Classification);

            Assert.AreEqual("Standard", classifications["new_standard"]);
            Assert.AreEqual("Intersect", classifications["new_intersect"]);
            Assert.AreEqual("BPF", classifications["new_bpf"]);
        }

        [TestMethod]
        public void LoadsOrganizationSsrsReportsAndTheirPublicationMetadataUsingReadOnlyQueries()
        {
            var reportId = Guid.Parse("12345678-aaaa-bbbb-cccc-1234567890ab");
            var report = new Entity("report", reportId);
            report["reportid"] = reportId;
            report["reportidunique"] = Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb");
            report["name"] = "Student summary";
            report["filename"] = "StudentSummary.rdl";
            report["reporttypecode"] = new OptionSetValue(1);
            report["ispersonal"] = false;
            report["bodytext"] = "\r\n<Report xmlns='urn:report'><DataSets><DataSet Name='Students' /></DataSets></Report>\r\n";
            report["defaultfilter"] = "<fetch><entity name='account' /></fetch>";

            var reportEntity = new Entity("reportentity");
            reportEntity["reportid"] = new EntityReference("report", reportId);
            reportEntity["objecttypecode"] = "account";
            var reportCategory = new Entity("reportcategory");
            reportCategory["reportid"] = new EntityReference("report", reportId);
            reportCategory["categorycode"] = new OptionSetValue(4);
            var reportVisibility = new Entity("reportvisibility");
            reportVisibility["reportid"] = new EntityReference("report", reportId);
            reportVisibility["visibilitycode"] = new OptionSetValue(2);
            var service = new RecordingService();
            service.RetrieveResults["report"] = new EntityCollection(new List<Entity> { report });
            service.RetrieveResults["reportentity"] = new EntityCollection(new List<Entity> { reportEntity });
            service.RetrieveResults["reportcategory"] = new EntityCollection(new List<Entity> { reportCategory });
            service.RetrieveResults["reportvisibility"] = new EntityCollection(new List<Entity> { reportVisibility });

            var snapshot = new DataverseMetadataService().LoadSnapshot(
                service,
                ComparisonAreas.Reports,
                false);

            Assert.AreEqual(1, snapshot.Reports.Count);
            var loaded = snapshot.Reports.Single();
            Assert.AreEqual($"report|id:{reportId:D}", loaded.Key);
            Assert.AreEqual("Student summary", loaded.Name);
            Assert.AreEqual("account", loaded.GetProperty("Associated tables"));
            Assert.AreEqual("4", loaded.GetProperty("Categories"));
            Assert.AreEqual("2", loaded.GetProperty("Visibility"));
            StringAssert.StartsWith(loaded.GetProperty("RDL"), "<Report");
            StringAssert.StartsWith(loaded.GetProperty("Raw RDL"), "\r\n<Report");
            CollectionAssert.AreEquivalent(
                new[] { "report", "reportentity", "reportcategory", "reportvisibility" },
                service.RetrievedEntityNames.ToArray());
            var reportQuery = service.RetrievedQueries.Single(item => item.EntityName == "report");
            Assert.AreEqual(25, reportQuery.PageInfo.Count);
            Assert.IsTrue(reportQuery.Criteria.Conditions.Any(condition =>
                condition.AttributeName == "ispersonal"
                && condition.Operator == ConditionOperator.Equal
                && condition.Values.Cast<object>().Single().Equals(false)));
            Assert.IsTrue(reportQuery.Criteria.Conditions.Any(condition =>
                condition.AttributeName == "reporttypecode"
                && condition.Operator == ConditionOperator.Equal
                && condition.Values.Cast<object>().Single().Equals(1)));
            Assert.AreEqual(0, service.WriteAttempts);
        }

        [TestMethod]
        public void IncludeUnpublishedUsesReadOnlyUnpublishedRequestsForReportsAndRelatedRecords()
        {
            var reportId = Guid.Parse("12345678-aaaa-bbbb-cccc-1234567890ab");
            var report = new Entity("report", reportId);
            report["reportid"] = reportId;
            report["name"] = "Student summary";
            report["reporttypecode"] = new OptionSetValue(1);
            report["ispersonal"] = false;
            report["bodytext"] = "<Report />";
            var service = new RecordingService();
            service.UnpublishedRetrieveResults["report"] = new EntityCollection(new List<Entity> { report });
            service.UnpublishedRetrieveResults["reportentity"] = new EntityCollection();
            service.UnpublishedRetrieveResults["reportcategory"] = new EntityCollection();
            service.UnpublishedRetrieveResults["reportvisibility"] = new EntityCollection();

            var snapshot = new DataverseMetadataService().LoadSnapshot(
                service,
                ComparisonAreas.Reports,
                true);

            Assert.AreEqual(1, snapshot.Reports.Count);
            Assert.IsTrue(snapshot.IncludesUnpublishedMetadata);
            CollectionAssert.AreEquivalent(
                new[] { "report", "reportentity", "reportcategory", "reportvisibility" },
                service.UnpublishedRetrievedEntityNames.ToArray());
            Assert.AreEqual(25, service.RetrievedQueries.Single(item => item.EntityName == "report").PageInfo.Count);
            Assert.AreEqual(0, service.RetrievedEntityNames.Count);
            Assert.AreEqual(0, service.WriteAttempts);
        }

        [TestMethod]
        public void LoadsCloudFlowsBusinessRulesAndWorkflowsFromSelectedReadOnlyProcessCategories()
        {
            var cloudFlow = Process(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                5,
                "contoso_cloudflow",
                "Student notification flow",
                "none");
            cloudFlow["clientdata"] = "{\"trigger\":{\"type\":\"Dataverse\"},\"actions\":[\"Notify\"]}";
            var businessRule = Process(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                2,
                "contoso_businessrule",
                "Require student code",
                "contact");
            businessRule["xaml"] = "<Activity><Sequence><Assign /></Sequence></Activity>";
            var workflow = Process(
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                0,
                "contoso_workflow",
                "Update account status",
                "account");
            workflow["triggeroncreate"] = true;
            var service = new RecordingService();
            service.RetrieveResults["workflow"] = new EntityCollection(new List<Entity> { cloudFlow, businessRule, workflow });

            var snapshot = new DataverseMetadataService().LoadSnapshot(
                service,
                ComparisonAreas.CloudFlows | ComparisonAreas.BusinessRules | ComparisonAreas.Workflows,
                false);

            Assert.AreEqual(3, snapshot.Processes.Count);
            CollectionAssert.AreEquivalent(
                new[] { ComparisonScope.CloudFlow, ComparisonScope.BusinessRule, ComparisonScope.Workflow },
                snapshot.Processes.Select(process => process.Scope).ToArray());
            Assert.AreEqual(string.Empty, snapshot.Processes.Single(process => process.Scope == ComparisonScope.CloudFlow).TableLogicalName);
            Assert.AreEqual("contact", snapshot.Processes.Single(process => process.Scope == ComparisonScope.BusinessRule).TableLogicalName);
            StringAssert.StartsWith(snapshot.Processes.Single(process => process.Scope == ComparisonScope.CloudFlow).GetProperty("Client data"), "{");
            var query = service.RetrievedQueries.Single(item => item.EntityName == "workflow");
            Assert.AreEqual(100, query.PageInfo.Count);
            CollectionAssert.Contains(query.ColumnSet.Columns, "workflowidunique");
            CollectionAssert.Contains(query.ColumnSet.Columns, "clientdata");
            CollectionAssert.Contains(query.ColumnSet.Columns, "xaml");
            Assert.IsTrue(query.Criteria.Conditions.Any(condition =>
                condition.AttributeName == "category"
                && condition.Operator == ConditionOperator.In
                && condition.Values.Cast<object>().Select(Convert.ToInt32).OrderBy(value => value).SequenceEqual(new[] { 0, 2, 5 })));
            Assert.IsTrue(query.Criteria.Conditions.Any(condition =>
                condition.AttributeName == "type"
                && condition.Operator == ConditionOperator.Equal
                && Convert.ToInt32(condition.Values.Single()) == 1));
            Assert.IsTrue(query.Criteria.Conditions.Any(condition =>
                condition.AttributeName == "componentstate"
                && condition.Operator == ConditionOperator.In));
            Assert.AreEqual(0, service.WriteAttempts);
        }

        [TestMethod]
        public void IncludeUnpublishedUsesReadOnlyUnpublishedRequestForProcesses()
        {
            var service = new RecordingService();
            service.UnpublishedRetrieveResults["workflow"] = new EntityCollection(new List<Entity>
            {
                Process(
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    5,
                    "contoso_cloudflow",
                    "Student notification flow",
                    "none")
            });

            var snapshot = new DataverseMetadataService().LoadSnapshot(service, ComparisonAreas.CloudFlows, true);

            Assert.AreEqual(1, snapshot.Processes.Count);
            CollectionAssert.AreEqual(new[] { "workflow" }, service.UnpublishedRetrievedEntityNames.ToArray());
            Assert.AreEqual(0, service.RetrievedEntityNames.Count);
            Assert.AreEqual(100, service.RetrievedQueries.Single().PageInfo.Count);
            Assert.AreEqual(0, service.WriteAttempts);
        }

        [TestMethod]
        public void StructuredDefinitionNormalizationIgnoresJsonPropertyOrderAndFormatting()
        {
            const string first = "{\"trigger\":{\"type\":\"Dataverse\",\"table\":\"account\"},\"enabled\":true}";
            const string second = "{\n  \"enabled\": true,\n  \"trigger\": { \"table\": \"account\", \"type\": \"Dataverse\" }\n}";

            Assert.AreEqual(
                DataverseMetadataService.NormalizeStructuredDefinition(first),
                DataverseMetadataService.NormalizeStructuredDefinition(second));
            Assert.AreEqual(
                DataverseMetadataService.DefinitionFingerprint(first),
                DataverseMetadataService.DefinitionFingerprint(second));
        }

        [TestMethod]
        public void XmlNormalizationIgnoresFormattingAndAttributeOrderAndRetainsFullXml()
        {
            const string first = "<form a='1' b='2'><tab>Value</tab></form>";
            const string second = "<form b='2' a='1'>\r\n  <tab>Value</tab>\r\n</form>";
            var normalizedFirst = DataverseMetadataService.NormalizeDefinition(first);
            var normalizedSecond = DataverseMetadataService.NormalizeDefinition(second);

            Assert.AreEqual(normalizedFirst, normalizedSecond);
            StringAssert.StartsWith(normalizedFirst, "<form");
            StringAssert.Contains(normalizedFirst, "<tab>Value</tab>");
            Assert.AreEqual(
                DataverseMetadataService.DefinitionFingerprint(first),
                DataverseMetadataService.DefinitionFingerprint(second));
        }

        [TestMethod]
        public void FormNormalizationIgnoresGeneratedLabelIdsAndEmptyPlaceholderCellIds()
        {
            const string environmentA =
                "<form><tabs><tab id='{11111111-1111-1111-1111-111111111111}' labelid='{aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa}'>" +
                "<labels><label description='Summary' languagecode='1033' /></labels>" +
                "<columns><column><sections><section id='{22222222-2222-2222-2222-222222222222}' labelid='{bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb}'>" +
                "<labels><label description='Details' languagecode='1033' /></labels><rows><row>" +
                "<cell id='{33333333-3333-3333-3333-333333333333}' labelid='{cccccccc-cccc-cccc-cccc-cccccccccccc}'>" +
                "<labels><label description='' languagecode='1033' /></labels></cell>" +
                "</row></rows></section></sections></column></columns></tab></tabs></form>";
            const string environmentB =
                "<form><tabs><tab id='{11111111-1111-1111-1111-111111111111}' labelid='{dddddddd-dddd-dddd-dddd-dddddddddddd}'>" +
                "<labels><label description='Summary' languagecode='1033' /></labels>" +
                "<columns><column><sections><section id='{22222222-2222-2222-2222-222222222222}' labelid='{eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee}'>" +
                "<labels><label description='Details' languagecode='1033' /></labels><rows><row>" +
                "<cell id='{44444444-4444-4444-4444-444444444444}' labelid='{ffffffff-ffff-ffff-ffff-ffffffffffff}'>" +
                "<labels><label description='' languagecode='1033' /></labels></cell>" +
                "</row></rows></section></sections></column></columns></tab></tabs></form>";

            Assert.AreEqual(
                DataverseMetadataService.NormalizeFormDefinition(environmentA),
                DataverseMetadataService.NormalizeFormDefinition(environmentB));
        }

        [TestMethod]
        public void FormNormalizationRetainsIdsOnMeaningfulCells()
        {
            const string environmentA =
                "<form><cell id='{aaaaaaaa-1111-1111-1111-111111111111}' labelid='{cccccccc-3333-3333-3333-333333333333}'>" +
                "<labels><label description='Name' languagecode='1033' /></labels>" +
                "<control id='name' datafieldname='name' /></cell></form>";
            const string environmentB =
                "<form><cell id='{bbbbbbbb-2222-2222-2222-222222222222}' labelid='{dddddddd-4444-4444-4444-444444444444}'>" +
                "<labels><label description='Name' languagecode='1033' /></labels>" +
                "<control id='name' datafieldname='name' /></cell></form>";

            Assert.AreNotEqual(
                DataverseMetadataService.NormalizeFormDefinition(environmentA),
                DataverseMetadataService.NormalizeFormDefinition(environmentB));
        }

        [TestMethod]
        public void RollupFormulaWhitespaceDoesNotCreateADifference()
        {
            const string formatted =
                "<Activity xmlns='http://schemas.microsoft.com/netfx/2009/xaml/activities' DisplayName='Open Revenue'>\r\n" +
                "  <Sequence>\r\n" +
                "    <Variable x:TypeArguments='x:Decimal' Name='Result' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' />\r\n" +
                "    <Assign />\r\n" +
                "  </Sequence>\r\n" +
                "</Activity>";
            const string compact =
                "<Activity DisplayName='Open Revenue' xmlns='http://schemas.microsoft.com/netfx/2009/xaml/activities'><Sequence><Variable Name='Result' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' x:TypeArguments='x:Decimal'/><Assign/></Sequence></Activity>";

            Assert.AreEqual(
                DataverseMetadataService.NormalizeDefinition(formatted),
                DataverseMetadataService.NormalizeDefinition(compact));
        }

        private static EntityMetadata Entity(string logicalName, bool isIntersect, bool isBpfEntity)
        {
            var metadata = new EntityMetadata
            {
                LogicalName = logicalName,
                IsBPFEntity = isBpfEntity
            };
            typeof(EntityMetadata)
                .GetProperty("IsIntersect")!
                .GetSetMethod(true)!
                .Invoke(metadata, new object[] { (bool?)isIntersect });
            return metadata;
        }

        private static EntityCollection Forms(Guid formId, Guid formIdUnique)
        {
            var form = new Entity("systemform", formId);
            form["formid"] = formId;
            form["formidunique"] = formIdUnique;
            form["objecttypecode"] = "account";
            form["name"] = "Information";
            form["formxml"] = "\r\n<form />\r\n";
            return new EntityCollection(new List<Entity> { form });
        }

        private static Entity Process(Guid id, int category, string uniqueName, string name, string primaryEntity)
        {
            var process = new Entity("workflow", id);
            process["workflowid"] = id;
            process["workflowidunique"] = Guid.NewGuid();
            process["category"] = new OptionSetValue(category);
            process["type"] = new OptionSetValue(1);
            process["componentstate"] = new OptionSetValue(0);
            process["uniquename"] = uniqueName;
            process["name"] = name;
            process["primaryentity"] = primaryEntity;
            process["statecode"] = new OptionSetValue(1);
            process["statuscode"] = new OptionSetValue(2);
            return process;
        }

        private static EntityCollection FormWithRole(Guid formId, Guid roleId)
        {
            var form = new Entity("systemform", formId);
            form["formid"] = formId;
            form["objecttypecode"] = "account";
            form["name"] = "Information";
            form["formxml"] = "<form><DisplayConditions FallbackForm='true' Order='1'><Role Id='{" +
                roleId.ToString("D") +
                "}' /></DisplayConditions></form>";
            return new EntityCollection(new List<Entity> { form });
        }

        private static Entity Role(Guid roleId, Guid templateId, string name)
        {
            var role = new Entity("role", roleId);
            role["name"] = name;
            role["roletemplateid"] = new EntityReference("roletemplate", templateId);
            role["parentrootroleid"] = new EntityReference("role", roleId);
            return role;
        }

        private static Entity RoleWithoutTemplate(Guid roleId, Guid rootRoleId, string name)
        {
            var role = new Entity("role", roleId);
            role["name"] = name;
            role["parentrootroleid"] = new EntityReference("role", rootRoleId);
            return role;
        }

        private static EntityCollection Roles(params Entity[] roles)
        {
            return new EntityCollection(roles.ToList());
        }

        private sealed class RecordingService : IOrganizationService
        {
            public EntityMetadata[] EntityMetadata { get; set; } = Array.Empty<EntityMetadata>();

            public List<OrganizationRequest> ExecuteRequests { get; } = new List<OrganizationRequest>();

            public List<string> RetrievedEntityNames { get; } = new List<string>();

            public List<QueryExpression> RetrievedQueries { get; } = new List<QueryExpression>();

            public List<string> UnpublishedRetrievedEntityNames { get; } = new List<string>();

            public Dictionary<string, EntityCollection> RetrieveResults { get; } =
                new Dictionary<string, EntityCollection>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, EntityCollection> UnpublishedRetrieveResults { get; } =
                new Dictionary<string, EntityCollection>(StringComparer.OrdinalIgnoreCase);

            public int WriteAttempts { get; private set; }

            public OrganizationResponse Execute(OrganizationRequest request)
            {
                ExecuteRequests.Add(request);
                if (request is RetrieveUnpublishedMultipleRequest unpublishedRequest)
                {
                    var query = (QueryExpression)unpublishedRequest.Query;
                    UnpublishedRetrievedEntityNames.Add(query.EntityName);
                    RetrievedQueries.Add(query);
                    var entities = UnpublishedRetrieveResults.TryGetValue(query.EntityName, out var result)
                        ? result
                        : new EntityCollection();
                    var unpublishedResponse = new RetrieveUnpublishedMultipleResponse();
                    unpublishedResponse.Results["EntityCollection"] = entities;
                    return unpublishedResponse;
                }

                var response = new RetrieveAllEntitiesResponse();
                response.Results["EntityMetadata"] = EntityMetadata;
                return response;
            }

            public EntityCollection RetrieveMultiple(QueryBase query)
            {
                var expression = (QueryExpression)query;
                RetrievedEntityNames.Add(expression.EntityName);
                RetrievedQueries.Add(expression);
                return RetrieveResults.TryGetValue(expression.EntityName, out var result)
                    ? result
                    : new EntityCollection();
            }

            public Guid Create(Entity entity)
            {
                WriteAttempts++;
                throw new InvalidOperationException("Writes are not supported.");
            }

            public void Update(Entity entity)
            {
                WriteAttempts++;
                throw new InvalidOperationException("Writes are not supported.");
            }

            public void Delete(string entityName, Guid id)
            {
                WriteAttempts++;
                throw new InvalidOperationException("Writes are not supported.");
            }

            public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
            {
                throw new NotSupportedException();
            }

            public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
            {
                WriteAttempts++;
                throw new InvalidOperationException("Writes are not supported.");
            }

            public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
            {
                WriteAttempts++;
                throw new InvalidOperationException("Writes are not supported.");
            }
        }
    }
}
