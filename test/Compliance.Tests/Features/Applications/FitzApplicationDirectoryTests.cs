using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class FitzApplicationDirectoryTests
{
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
        var now = DateTimeOffset.UtcNow;
        var identity = new CheckpointIdentity("ApplicationDirectoryV2",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, legacyActor, "Original", now));
            await directory.ApplyAsync(new SystemInstanceDeclared(tenantId,
                applicationId, legacyId, 2, "Legacy", "production", null,
                "legacy-source", legacyActor, "Legacy actor", now.AddMinutes(1)));
            await directory.ApplyAsync(new ApplicationRevised(tenantId, applicationId,
                3, "Payroll", "Run monthly payroll", null, metadataActor,
                "Metadata actor", now.AddMinutes(2)));
            await directory.ApplyAsync(new SystemInstanceRegistered(tenantId,
                applicationId, newId, 1, "New", "staging", null,
                "new-source", instanceActor, "Instance actor", now.AddMinutes(3)));
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
        Assert.Equal([1L, 2L, 3L], history?.Items.Select(item => item.Revision));
        Assert.Equal("system_instance_declared", history?.Items[1].ChangeKind);
        Assert.Equal(legacyId, history?.Items[1].SystemInstanceId);
        Assert.Equal(legacyActor, legacy?.DeclaredByMemberId);
        Assert.Equal(2, legacy?.LegacyApplicationRevision);
        Assert.Equal(1, legacy?.Revision);
        Assert.Equal(instanceActor, current?.DeclaredByMemberId);
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
}
