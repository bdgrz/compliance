using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationBoundaryReferenceDirectoryTests
{
    [Fact]
    public async Task ShouldPreserveApprovedHistoryGivenBoundaryReferenceTransitions()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var firstVersion = Uuid.CreateVersion4();
        var successorVersion = Uuid.CreateVersion4();
        var applicationEntry = Entry("application", applicationId);
        var instanceEntry = Entry("system_instance", instanceId);
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var directory = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var identity = Identity(tenantId);

        await ApplyAsync(directory, identity, new BoundaryDraftCreated(tenantId,
            boundaryId, programId, firstVersion, Content(applicationEntry, instanceEntry),
            Uuid.CreateVersion4(), "Author", now));
        Assert.Single((await directory.ListAsync(tenantId, "application", applicationId,
            20, null)).Items);
        Assert.Single((await directory.ListAsync(tenantId, "system_instance", instanceId,
            20, null)).Items);

        // Act
        await ApplyAsync(directory, identity, new BoundaryDraftRevised(tenantId,
            boundaryId, firstVersion, 2, Content(instanceEntry), Uuid.CreateVersion4(),
            "Author", now.AddMinutes(1)));
        var removedDraft = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);
        await ApplyAsync(directory, identity, new BoundaryApproved(tenantId,
            boundaryId, firstVersion, 2, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), "Approver", "Approved", new DateOnly(2026, 9, 20),
            now.AddMinutes(2), "digest"));
        await ApplyAsync(directory, identity, new BoundarySuccessorProposed(tenantId,
            boundaryId, successorVersion, firstVersion, Content(applicationEntry),
            Uuid.CreateVersion4(), "Author", now.AddMinutes(3)));
        var proposed = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);
        await ApplyAsync(directory, identity, new BoundaryApproved(tenantId,
            boundaryId, successorVersion, 1, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), "Approver", "Approved", new DateOnly(2026, 9, 21),
            now.AddMinutes(4), "digest-2"));
        var current = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);
        var historical = await directory.ListAsync(tenantId, "system_instance", instanceId,
            20, null);
        var otherTenant = await directory.ListAsync(otherTenantId, "application",
            applicationId, 20, null);

        // Assert
        Assert.Empty(removedDraft.Items);
        Assert.Equal("draft", Assert.Single(proposed.Items).Status);
        Assert.Equal("approved", Assert.Single(current.Items).Status);
        Assert.Equal(successorVersion, current.Items[0].VersionId);
        Assert.Equal("historical", Assert.Single(historical.Items).Status);
        Assert.Equal(firstVersion, historical.Items[0].VersionId);
        Assert.Equal(new DateOnly(2026, 9, 20), historical.Items[0].EffectiveFrom);
        Assert.Empty(otherTenant.Items);
    }

    [Fact]
    public async Task ShouldRollBackReferenceRowsGivenFailedProjectionBatch()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        DomainEvent created = new BoundaryDraftCreated(tenantId, boundaryId, Uuid.CreateVersion4(),
            versionId, Content(Entry("application", applicationId)), Uuid.CreateVersion4(),
            "Author", DateTimeOffset.UtcNow);
        DomainEvent invalid = new BoundaryDraftRevised(tenantId, boundaryId,
            Uuid.CreateVersion4(), 2, Content(), Uuid.CreateVersion4(),
            "Author", DateTimeOffset.UtcNow);
        var directory = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var identity = Identity(tenantId);

        // Act
        await using (var failed = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(invalid));
        }
        var afterFailure = await directory.ListAsync(tenantId, "application",
            applicationId, 20, null);
        await ApplyAsync(directory, identity, created);
        var afterRetry = await directory.ListAsync(tenantId, "application",
            applicationId, 20, null);

        // Assert
        Assert.Empty(afterFailure.Items);
        Assert.Single(afterRetry.Items);
        Assert.Equal(ProjectionCheckpoint.Start,
            await directory.LoadCheckpointAsync(tenantId));
    }

    [Fact]
    public async Task ShouldRemoveDraftReferenceGivenSuccessorDiscard()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var firstVersion = Uuid.CreateVersion4();
        var successorVersion = Uuid.CreateVersion4();
        var directory = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var identity = Identity(tenantId);
        var now = DateTimeOffset.UtcNow;
        await ApplyAsync(directory, identity, new BoundaryDraftCreated(tenantId,
            boundaryId, programId, firstVersion, Content(Entry("application", applicationId)),
            Uuid.CreateVersion4(), "Author", now));
        await ApplyAsync(directory, identity, new BoundaryApproved(tenantId,
            boundaryId, firstVersion, 1, Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), "Approver", "Approved", new DateOnly(2026, 9, 20),
            now, "digest"));
        await ApplyAsync(directory, identity, new BoundarySuccessorProposed(tenantId,
            boundaryId, successorVersion, firstVersion,
            Content(Entry("application", applicationId)), Uuid.CreateVersion4(),
            "Author", now));

        // Act
        await ApplyAsync(directory, identity, new BoundaryDraftDiscarded(tenantId,
            boundaryId, successorVersion, 1, Uuid.CreateVersion4(), "Author",
            "Discarded", now));
        var result = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);

        // Assert
        Assert.Equal("approved", Assert.Single(result.Items).Status);
        Assert.Equal(firstVersion, result.Items[0].VersionId);
    }

    [Fact]
    public async Task ShouldReportLagThenAllowEmptyReadGivenSourceCheckpointCatchup()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var directory = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var events = new InMemoryEventStore();
        var consistency = new ApplicationBoundaryReferenceReadConsistency(directory, events);
        var stream = new EventStreamAddress(tenantId.ToString(), "boundaries",
            boundaryId.ToString());
        DomainEvent created = new BoundaryDraftCreated(tenantId, boundaryId,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), Content(), Uuid.CreateVersion4(),
            "Author", DateTimeOffset.UtcNow);
        created.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), boundaryId,
            1, DateTimeOffset.UtcNow));

        // Act
        var noSource = await consistency.EnsureCaughtUpAsync(tenantId, CancellationToken.None);
        await events.AppendAsync(stream, 0, [created]);
        var lagged = await consistency.EnsureCaughtUpAsync(tenantId, CancellationToken.None);
        await using var source = events.ReadAsync(
            EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries"),
            EventCursor.Start, CancellationToken.None).GetAsyncEnumerator();
        Assert.True(await source.MoveNextAsync());
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await batch.CommitAsync(new ProjectionCheckpoint(source.Current.NextCursor));
        }
        var caughtUp = await consistency.EnsureCaughtUpAsync(tenantId,
            CancellationToken.None);

        // Assert
        Assert.True(noSource.IsSuccess);
        Assert.False(lagged.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, lagged.Error.Kind);
        Assert.True(caughtUp.IsSuccess);
        Assert.Empty((await directory.ListAsync(tenantId, "application",
            Uuid.CreateVersion4(), 20, null)).Items);
    }

    static BoundaryScopeEntry Entry(string type, Uuid recordId) =>
        new(Uuid.CreateVersion4(), "inclusion", type, "Governed subject", recordId,
            "owner", "In scope", false);

    static BoundaryContent Content(params BoundaryScopeEntry[] entries) =>
        new("Scoped system", "readiness", ["security"], entries);

    static CheckpointIdentity Identity(Uuid tenantId) =>
        new("ApplicationBoundaryReferencesV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries"));

    static async Task ApplyAsync(FitzApplicationBoundaryReferenceDirectory directory,
        CheckpointIdentity identity, DomainEvent domainEvent)
    {
        await using var batch = await directory.BeginAsync(
            new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
