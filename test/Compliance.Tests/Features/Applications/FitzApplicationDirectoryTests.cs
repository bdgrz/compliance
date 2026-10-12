using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class FitzApplicationDirectoryTests
{
    [Fact]
    public async Task ShouldRetainDeclarationAdditionsAndActualGapsGivenLegacyRevisionEvent()
    {
        // Arrange
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var ownerId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var declaredActor = new ActorReference("member", "original-declarer", "Original declarer");
        var revisedActor = new ActorReference("member", "legacy-editor", "Legacy editor");
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, actorId, "Declarer", now, "internal",
                ownerId, ownerId, true, "service", "critical")
            {
                StoredActor = declaredActor,
            });
            var oldPayload = JsonSerializer.SerializeToNode(new ApplicationRevised(tenantId, applicationId, 2,
                "Payroll", "Run monthly payroll", null, actorId, "Editor", now.AddMinutes(1))
            {
                StoredActor = revisedActor,
            }, ComplianceCoreJsonContext.Default.ApplicationRevised)!.AsObject();
            oldPayload.Remove("application_kind");
            oldPayload.Remove("criticality");
            await directory.ApplyAsync(oldPayload.Deserialize(ComplianceCoreJsonContext.Default.ApplicationRevised)!);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var current = await directory.GetAsync(tenantId, applicationId);
        var declared = await directory.GetRevisionAsync(tenantId, applicationId, 1);
        var revised = await directory.GetRevisionAsync(tenantId, applicationId, 2);
        var applications = await directory.ListAsync(tenantId, 1, null);
        var firstPage = await directory.ListRevisionsAsync(tenantId, applicationId, 1, null);
        var nextPage = await directory.ListRevisionsAsync(tenantId, applicationId, 1,
            firstPage?.NextCursor);

        // Assert
        Assert.Equal("service", current?.ApplicationKind);
        Assert.Equal("critical", current?.Criticality);
        Assert.Equal("active", current?.Lifecycle);
        Assert.Equal("manual", current?.SourceKind);
        Assert.Equal(applicationId.ToString(), current?.SourceIdentifier);
        Assert.Equal(revisedActor, current?.LastChangedBy);
        Assert.Equal(2, current?.Revision);
        Assert.Null(current?.Classification);
        Assert.Null(current?.SystemOwnerPersonId);
        Assert.Null(current?.AccessOwnerPersonId);
        Assert.Contains("classification_unresolved", current!.Unresolved);
        Assert.Contains("system_owner_missing", current.Unresolved);
        Assert.Contains("access_owner_missing", current.Unresolved);
        Assert.DoesNotContain("application_kind_missing", current.Unresolved);
        Assert.DoesNotContain("criticality_missing", current.Unresolved);
        Assert.Equal("internal", declared?.Classification);
        Assert.Equal(ownerId, declared?.SystemOwnerPersonId);
        Assert.Equal(ownerId, declared?.AccessOwnerPersonId);
        Assert.Equal(declaredActor, declared?.Actor);
        Assert.True(declared?.IsRestricted);
        Assert.Equal("Run payroll", declared?.Purpose);
        Assert.Contains("classification_unverified", declared!.Unresolved);
        Assert.Contains("owner_missing", declared.Unresolved);
        Assert.Contains("system_instances_missing", declared.Unresolved);
        Assert.DoesNotContain("system_owner_missing", declared.Unresolved);
        Assert.DoesNotContain("access_owner_missing", declared.Unresolved);
        Assert.Equal("service", declared.ApplicationKind);
        Assert.Equal("critical", declared.Criticality);
        Assert.Equal("service", revised?.ApplicationKind);
        Assert.Equal("critical", revised?.Criticality);
        Assert.Equal(revisedActor, revised?.Actor);
        Assert.Contains("classification_unresolved", revised!.Unresolved);
        var listed = Assert.Single(applications.Items);
        Assert.Equal(current.ApplicationKind, listed.ApplicationKind);
        Assert.Equal(current.Criticality, listed.Criticality);
        Assert.Equal(current.Unresolved, listed.Unresolved);
        var listedDeclaration = Assert.Single(firstPage!.Items);
        Assert.Equal(declared.ApplicationKind, listedDeclaration.ApplicationKind);
        Assert.Equal(declared.Criticality, listedDeclaration.Criticality);
        Assert.Equal(declared.Unresolved, listedDeclaration.Unresolved);
        Assert.NotNull(firstPage.NextCursor);
        var listedRevision = Assert.Single(nextPage!.Items);
        Assert.Equal(revised.ApplicationKind, listedRevision.ApplicationKind);
        Assert.Equal(revised.Criticality, listedRevision.Criticality);
        Assert.Equal(revised.Unresolved, listedRevision.Unresolved);
        Assert.Null(nextPage.NextCursor);
    }

    [Fact]
    public async Task ShouldExposeMissingAdditionsWithoutRewritingGivenLegacySerializedRows()
    {
        // Arrange
        var client = new LegacyRowsClient(new InMemoryKvClient());
        var directory = new FitzApplicationDirectory(client);
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", "Operations", actorId, "Original", now,
                IsRestricted: true));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var writesBeforeRead = client.Writes;

        // Act
        var current = await directory.GetAsync(tenantId, applicationId);
        var exact = await directory.GetRevisionAsync(tenantId, applicationId, 1);
        var applications = await directory.ListAsync(tenantId, 1, null);
        var history = await directory.ListRevisionsAsync(tenantId, applicationId, 1, null);
        var storedPayloads = await client.ReadStoredRowsAsync();

        // Assert
        // Current and revision rows each have a primary and an indexed serialized payload.
        Assert.Equal(4, client.Rows.Count);
        Assert.Equal(writesBeforeRead, client.Writes);
        for (var index = 0; index < client.Rows.Count; index++)
        {
            var row = client.Rows[index];
            var payload = JsonNode.Parse(row.Payload)!.AsObject();
            Assert.False(payload.ContainsKey("application_kind"));
            Assert.False(payload.ContainsKey("criticality"));
            Assert.DoesNotContain("application_kind_missing", payload["unresolved"]!.AsArray()
                .Select(value => value!.GetValue<string>()));
            Assert.Equal(row.Payload, storedPayloads[index]);
        }
        Assert.NotNull(current);
        Assert.Null(current.ApplicationKind);
        Assert.Null(current.Criticality);
        Assert.Equal(1, current.Revision);
        Assert.Equal("active", current.Lifecycle);
        Assert.True(current.IsRestricted);
        Assert.Equal("manual", current.SourceKind);
        Assert.Equal(applicationId.ToString(), current.SourceIdentifier);
        Assert.Equal(ActorReference.ForMember(actorId, "Original"), current.LastChangedBy);
        Assert.Equal(now, current.LastChangedAt);
        Assert.Equal(["owner_unverified", "classification_unresolved", "system_instances_missing",
            "system_owner_missing", "access_owner_missing", "legacy_review_pending",
            "application_kind_missing", "criticality_missing"], current.Unresolved);
        Assert.Equal(current.Unresolved, exact?.Unresolved);
        Assert.Equal(1, exact?.Revision);
        Assert.Equal("active", exact?.Lifecycle);
        Assert.Equal("declared", exact?.ChangeKind);
        Assert.Equal(ActorReference.ForMember(actorId, "Original"), exact?.Actor);
        Assert.Null(exact?.ApplicationKind);
        Assert.Null(exact?.Criticality);
        var listed = Assert.Single(applications.Items);
        Assert.Equal(current.ApplicationId, listed.ApplicationId);
        Assert.Equal(current.Revision, listed.Revision);
        Assert.Equal(current.Unresolved, listed.Unresolved);
        Assert.Null(listed.ApplicationKind);
        Assert.Null(listed.Criticality);
        var listedRevision = Assert.Single(history!.Items);
        Assert.Equal(exact!.Revision, listedRevision.Revision);
        Assert.Equal(exact.Unresolved, listedRevision.Unresolved);
        Assert.Null(listedRevision.ApplicationKind);
        Assert.Null(listedRevision.Criticality);
    }

    [Fact]
    public async Task ShouldRetainRestrictionFlagGivenApplicationHistoryProjection()
    {
        // Arrange
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, actorId, "Org Admin", now,
                IsRestricted: true));
            await directory.ApplyAsync(new ApplicationRevised(tenantId, applicationId, 2,
                "Payroll", "Run payroll", null, actorId, "Org Admin", now.AddMinutes(1),
                IsRestricted: false));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var current = await directory.GetAsync(tenantId, applicationId);
        var restrictedRevision = await directory.GetRevisionAsync(tenantId, applicationId, 1);
        var publicRevision = await directory.GetRevisionAsync(tenantId, applicationId, 2);

        // Assert
        Assert.False(current?.IsRestricted);
        Assert.True(restrictedRevision?.IsRestricted);
        Assert.False(publicRevision?.IsRestricted);
    }

    [Fact]
    public async Task ShouldKeepApplicationHistoryGivenLegacyAndNewInstanceStreamReplay()
    {
        // Arrange
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var legacyId = Uuid.CreateVersion4();
        var newId = Uuid.CreateVersion4();
        var legacyActor = Uuid.CreateVersion4();
        var metadataActor = Uuid.CreateVersion4();
        var instanceActor = Uuid.CreateVersion4();
        var declaredSnapshot = new ActorReference("member", "former-declarer",
            "Declarer at creation");
        var revisedSnapshot = new ActorReference("member", "former-editor",
            "Editor at revision");
        var instanceSnapshot = new ActorReference("member", "former-instance-owner",
            "Instance owner at declaration");
        var now = DateTimeOffset.UtcNow;
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, legacyActor, "Original", now)
            {
                StoredActor = declaredSnapshot,
            });
            await directory.ApplyAsync(new SystemInstanceDeclared(tenantId,
                applicationId, legacyId, 2, "Legacy", "production", null,
                "legacy-source", legacyActor, "Legacy actor", now.AddMinutes(1)));
            await directory.ApplyAsync(new ApplicationRevised(tenantId, applicationId,
                3, "Payroll", "Run monthly payroll", null, metadataActor,
                "Metadata actor", now.AddMinutes(2))
            {
                StoredActor = revisedSnapshot,
            });
            await directory.ApplyAsync(new SystemInstanceRegistered(tenantId,
                applicationId, newId, 1, "New", "staging", null,
                "new-source", instanceActor, "Instance actor", now.AddMinutes(3))
            {
                StoredActor = instanceSnapshot,
            });
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var application = await directory.GetAsync(tenantId, applicationId);
        var legacy = await directory.GetInstanceAsync(tenantId, legacyId);
        var current = await directory.GetInstanceAsync(tenantId, newId);
        var history = await directory.ListRevisionsAsync(tenantId, applicationId, 10, null);

        // Assert
        Assert.Equal(3, application?.Revision);
        Assert.True(application?.HasSystemInstances);
        Assert.Equal(metadataActor, application?.LastChangedByMemberId);
        Assert.Equal(revisedSnapshot, application?.LastChangedBy);
        Assert.Equal([1L, 2L, 3L], history?.Items.Select(item => item.Revision));
        Assert.Equal(declaredSnapshot, history?.Items[0].Actor);
        Assert.Equal(revisedSnapshot, history?.Items[2].Actor);
        Assert.Equal("system_instance_declared", history?.Items[1].ChangeKind);
        Assert.Equal(legacyId, history?.Items[1].SystemInstanceId);
        Assert.Equal(legacyActor, legacy?.DeclaredByMemberId);
        Assert.Equal(ActorReference.ForMember(legacyActor, "Legacy actor"),
            legacy?.DeclaredBy);
        Assert.Equal(2, legacy?.LegacyApplicationRevision);
        Assert.Equal(1, legacy?.Revision);
        Assert.Equal(instanceActor, current?.DeclaredByMemberId);
        Assert.Equal(instanceSnapshot, current?.DeclaredBy);
        Assert.Equal(1, current?.Revision);
        Assert.Null(current?.LegacyApplicationRevision);
        Assert.Null(await directory.GetInstanceAsync(Uuid.CreateVersion4(), newId));
    }

    [Fact]
    public async Task ShouldKeepProjectingGivenLegacyAndRegisteredEventsWithSameInstanceKey()
    {
        // Arrange
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var laterId = Uuid.CreateVersion4();
        var legacyActor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, legacyActor, "Original", now));
            await directory.ApplyAsync(new SystemInstanceDeclared(tenantId, applicationId,
                instanceId, 2, "Production", "production", null, "payroll-prod",
                legacyActor, "Legacy actor", now));
            await directory.ApplyAsync(new SystemInstanceRegistered(tenantId, applicationId,
                instanceId, 1, "Production", "production", null, "payroll-prod",
                Uuid.CreateVersion4(), "New writer", now.AddMinutes(1)));
            await directory.ApplyAsync(new SystemInstanceRegistered(tenantId, applicationId,
                laterId, 1, "Staging", "staging", null, null,
                Uuid.CreateVersion4(), "New writer", now.AddMinutes(2)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var instance = await directory.GetInstanceAsync(tenantId, instanceId);
        var later = await directory.GetInstanceAsync(tenantId, laterId);

        // Assert
        Assert.Equal(legacyActor, instance?.DeclaredByMemberId);
        Assert.Equal(2, instance?.LegacyApplicationRevision);
        Assert.DoesNotContain("declaration_conflict", instance!.Unresolved);
        Assert.NotNull(later);
    }

    [Fact]
    public async Task ShouldKeepFirstRowAndFlagConflictGivenDifferentContentForSameInstanceKey()
    {
        // Arrange
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var otherApplicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var legacyActor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, legacyActor, "Original", now));
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, otherApplicationId,
                "Benefits", "Run benefits", null, legacyActor, "Original", now));
            await directory.ApplyAsync(new SystemInstanceDeclared(tenantId, applicationId,
                instanceId, 2, "Production", "production", null, "payroll-prod",
                legacyActor, "Legacy actor", now));
            await directory.ApplyAsync(new SystemInstanceRegistered(tenantId,
                otherApplicationId, instanceId, 1, "Other", "production", null, null,
                Uuid.CreateVersion4(), "New writer", now.AddMinutes(1)));
            await directory.ApplyAsync(new SystemInstanceDeclared(tenantId, applicationId,
                instanceId, 3, "Renamed", "production", null, "payroll-prod",
                legacyActor, "Legacy actor", now.AddMinutes(2)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var instance = await directory.GetInstanceAsync(tenantId, instanceId);
        var other = await directory.GetAsync(tenantId, otherApplicationId);
        var otherInstances = await directory.ListInstancesAsync(tenantId, otherApplicationId,
            10, null);
        var history = await directory.ListRevisionsAsync(tenantId, applicationId, 10, null);

        // Assert
        Assert.Equal(applicationId, instance?.ApplicationId);
        Assert.Equal("Production", instance?.Name);
        Assert.Equal(legacyActor, instance?.DeclaredByMemberId);
        Assert.Contains("declaration_conflict", instance!.Unresolved);
        Assert.False(other?.HasSystemInstances);
        Assert.Empty(otherInstances.Items);
        Assert.Equal([1L, 2L, 3L], history?.Items.Select(item => item.Revision));
    }

    [Fact]
    public async Task ShouldPreserveTenantBoundaryAndUnresolvedFactsGivenManualDeclarations()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var directory = new FitzApplicationDirectory(client);
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var missingSourceInstanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, actorId, "Manager", now, "internal"));
            await directory.ApplyAsync(new SystemInstanceDeclared(tenantId, applicationId,
                instanceId, 2, "Production", "production", null, "payroll-prod",
                actorId, "Manager", now));
            await directory.ApplyAsync(new SystemInstanceDeclared(tenantId, applicationId,
                missingSourceInstanceId, 3, "Staging", "staging", null, null,
                actorId, "Manager", now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var application = await directory.GetAsync(tenantId, applicationId);
        var instance = await directory.GetInstanceAsync(tenantId, instanceId);
        var page = await directory.ListInstancesAsync(tenantId, applicationId, 10, null);

        // Assert
        Assert.Equal(3, application?.Revision);
        Assert.Contains("owner_missing", application!.Unresolved);
        Assert.Equal("internal", application.Classification);
        Assert.Contains("classification_unverified", application.Unresolved);
        Assert.Contains("access_boundary_review_pending", application.Unresolved);
        Assert.Equal("manual", application.SourceKind);
        Assert.Equal("manual", instance?.SourceKind);
        Assert.Equal("payroll-prod", instance?.SourceIdentifier);
        Assert.Contains("access_boundary_missing", instance!.Unresolved);
        Assert.Contains("source_identifier_unverified", instance.Unresolved);
        Assert.Equal(2, page.Items.Count);
        var missingSource = await directory.GetInstanceAsync(tenantId, missingSourceInstanceId);
        Assert.Contains("source_identifier_missing", missingSource!.Unresolved);
        Assert.Null(await directory.GetAsync(otherTenantId, applicationId));
        Assert.Null(await directory.GetInstanceAsync(otherTenantId, instanceId));
        Assert.Empty((await directory.ListAsync(otherTenantId, 10, null)).Items);
    }

    [Fact]
    public async Task ShouldRollbackAndRetryGivenInterruptedProjection()
    {
        // Arrange
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, actorId, "Manager", now));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(new ApplicationRevised(tenantId, Uuid.CreateVersion4(),
                    2, "Wrong", "Wrong", null, actorId, "Manager", now)));
        }

        // Assert
        Assert.Null(await directory.GetAsync(tenantId, applicationId));
        Assert.Null(await directory.GetRevisionAsync(tenantId, applicationId, 1));
        await using (var retry = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, actorId, "Manager", now));
            await retry.CommitAsync(ProjectionCheckpoint.Start);
        }
        Assert.NotNull(await directory.GetAsync(tenantId, applicationId));
        var history = await directory.ListRevisionsAsync(tenantId, applicationId, 10, null);
        Assert.Single(history!.Items);
    }

    [Fact]
    public async Task ShouldKeepEachApplicationRevisionGivenDeclarationRevisionAndInstance()
    {
        // Arrange
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, actorId, "Manager", now));
            await directory.ApplyAsync(new ApplicationRevised(tenantId, applicationId, 2,
                "Payroll", "Run monthly payroll", "Finance", actorId, "Manager",
                now.AddMinutes(1), "internal"));
            await directory.ApplyAsync(new SystemInstanceDeclared(tenantId, applicationId,
                instanceId, 3, "Production", "production", null, "payroll-prod",
                actorId, "Manager", now.AddMinutes(2)));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var first = await directory.GetRevisionAsync(tenantId, applicationId, 1);
        var second = await directory.GetRevisionAsync(tenantId, applicationId, 2);
        var third = await directory.GetRevisionAsync(tenantId, applicationId, 3);
        var firstPage = await directory.ListRevisionsAsync(tenantId, applicationId, 2, null);
        var nextPage = await directory.ListRevisionsAsync(tenantId, applicationId, 2,
            firstPage?.NextCursor);

        // Assert
        Assert.Equal("Run payroll", first?.Purpose);
        Assert.Equal("declared", first?.ChangeKind);
        Assert.False(first?.HasSystemInstances);
        Assert.Equal("Run monthly payroll", second?.Purpose);
        Assert.Equal("internal", second?.Classification);
        Assert.Equal("revised", second?.ChangeKind);
        Assert.Equal("system_instance_declared", third?.ChangeKind);
        Assert.Equal(instanceId, third?.SystemInstanceId);
        Assert.Equal("payroll-prod", third?.SystemInstance?.SourceIdentifier);
        Assert.True(third?.HasSystemInstances);
        Assert.Equal([1L, 2L], firstPage?.Items.Select(item => item.Revision));
        Assert.NotNull(firstPage?.NextCursor);
        Assert.Equal([3L], nextPage?.Items.Select(item => item.Revision));
        Assert.Null(await directory.GetRevisionAsync(otherTenantId, applicationId, 1));
        Assert.Null(await directory.ListRevisionsAsync(otherTenantId, applicationId, 2, null));
    }

    sealed class LegacyRowsClient(IKvClient inner) : IKvClient
    {
        public int Writes { get; private set; }
        public List<(string Route, byte[] Key, byte[] Payload)> Rows { get; } = [];

        public async Task<IKvTransaction> BeginAsync(string route, KvDurability durability,
            KvMode mode = KvMode.ReadWrite, CancellationToken ct = default) =>
            new LegacyTransaction(await inner.BeginAsync(route, durability, mode, ct), this);

        public Task<KvSubscription> SubscribeAsync(string pattern, CancellationToken ct = default) =>
            inner.SubscribeAsync(pattern, ct);

        public async Task<List<byte[]>> ReadStoredRowsAsync()
        {
            var payloads = new List<byte[]>();
            foreach (var row in Rows)
            {
                await using var tx = await inner.BeginAsync(row.Route, KvDurability.Sync, KvMode.ReadOnly);
                var stored = await tx.GetAsync(row.Key);
                payloads.Add(stored.Value!.Value.ToArray());
            }
            return payloads;
        }

        ReadOnlyMemory<byte> LegacyPayload(string route, ReadOnlyMemory<byte> key,
            ReadOnlyMemory<byte> value)
        {
            Writes++;
            if (value.IsEmpty || value.Span[0] != (byte)'{')
                return value;
            var row = JsonNode.Parse(value.Span)!.AsObject();
            if (!row.ContainsKey("application_id") || !row.ContainsKey("name"))
                return value;
            row.Remove("application_kind");
            row.Remove("criticality");
            var unresolved = row["unresolved"]!.AsArray();
            for (var index = unresolved.Count - 1; index >= 0; index--)
                if (unresolved[index]!.GetValue<string>() is "application_kind_missing" or "criticality_missing")
                    unresolved.RemoveAt(index);
            unresolved.Add("legacy_review_pending");
            var payload = System.Text.Encoding.UTF8.GetBytes(row.ToJsonString());
            Rows.Add((route, key.ToArray(), payload));
            return payload;
        }

        sealed class LegacyTransaction(IKvTransaction inner, LegacyRowsClient owner) : IKvTransaction
        {
            public string Route => inner.Route;
            public Task<KvGetResult> GetAsync(ReadOnlyMemory<byte> key, CancellationToken ct = default) =>
                inner.GetAsync(key, ct);
            public Task PutAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value,
                CancellationToken ct = default) =>
                inner.PutAsync(key, owner.LegacyPayload(Route, key, value), ct);
            public Task InsertAsync(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value,
                CancellationToken ct = default) =>
                inner.InsertAsync(key, owner.LegacyPayload(Route, key, value), ct);
            public Task DeleteAsync(ReadOnlyMemory<byte> key, CancellationToken ct = default) =>
                inner.DeleteAsync(key, ct);
            public Task DeleteRangeAsync(ReadOnlyMemory<byte> startKey, ReadOnlyMemory<byte> endKey,
                CancellationToken ct = default) => inner.DeleteRangeAsync(startKey, endKey, ct);
            public Task<KvScanResult> ScanAsync(KvScanQuery query, CancellationToken ct = default) =>
                inner.ScanAsync(query, ct);
            public Task CommitAsync(CancellationToken ct = default) => inner.CommitAsync(ct);
            public Task RollbackAsync(CancellationToken ct = default) => inner.RollbackAsync(ct);
            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
