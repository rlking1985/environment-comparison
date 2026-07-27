using System;
using System.Collections.Generic;
using System.Linq;
using EnvironmentComparison.Domain;
using EnvironmentComparison.Services;
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
        public void FormFallbackUsesFormIdRatherThanFormIdUnique()
        {
            var formId = Guid.Parse("12345678-1234-1234-1234-1234567890ab");
            var environmentA = new RecordingService();
            var environmentB = new RecordingService();
            environmentA.RetrieveResults["systemform"] = Forms(formId, Guid.NewGuid());
            environmentB.RetrieveResults["systemform"] = Forms(formId, Guid.NewGuid());

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
            CollectionAssert.DoesNotContain(
                environmentA.RetrievedQueries.Single().ColumnSet.Columns,
                "formidunique");
        }

        [TestMethod]
        public void MapsTableAndImportantColumnMetadataWithoutManagedState()
        {
            var entityMetadata = new EntityMetadata
            {
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
            Assert.AreEqual("Standard", snapshot.Tables[0].Classification);
            Assert.AreEqual("UserOwned", snapshot.Tables[0].GetProperty("Ownership type"));
            Assert.AreEqual("True", snapshot.Tables[0].GetProperty("Audit enabled"));
            Assert.AreEqual(1, snapshot.Tables[0].Columns.Count);
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
            form["formxml"] = "<form />";
            return new EntityCollection(new List<Entity> { form });
        }

        private sealed class RecordingService : IOrganizationService
        {
            public EntityMetadata[] EntityMetadata { get; set; } = Array.Empty<EntityMetadata>();

            public List<OrganizationRequest> ExecuteRequests { get; } = new List<OrganizationRequest>();

            public List<string> RetrievedEntityNames { get; } = new List<string>();

            public List<QueryExpression> RetrievedQueries { get; } = new List<QueryExpression>();

            public Dictionary<string, EntityCollection> RetrieveResults { get; } =
                new Dictionary<string, EntityCollection>(StringComparer.OrdinalIgnoreCase);

            public int WriteAttempts { get; private set; }

            public OrganizationResponse Execute(OrganizationRequest request)
            {
                ExecuteRequests.Add(request);
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
