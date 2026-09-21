using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationChangePreviewTests
{
    [Fact]
    public async Task ShouldReportKnownBoundaryAndPendingContextsGivenProposedRevision()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        var references = new FitzApplicationBoundaryReferenceDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(
            tenantId, applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now));
        DomainEvent boundary = new BoundaryDraftCreated(tenantId, boundaryId, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), new BoundaryContent("Payroll system", "readiness",
                ["security"], [new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion",
                    "application", "Payroll", applicationId, "Operations", "In scope", false)]),
            actorId, "Manager", now);
        await using (var batch = await references.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity("ApplicationBoundaryReferencesV1",
                             EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries")),
                         ProjectionCheckpoint.Start)))
        {
            await references.ApplyAsync(boundary);
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source),
            applications, references, new ApplicationBoundaryReferenceReadConsistency(
                references, new InMemoryEventStore()));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "revise", "Payroll v2",
                "Run payroll", "Finance", "internal"), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Complete);
        Assert.Equal(3, result.Value.Changes.Count);
        Assert.Equal(["name", "owner_reference", "classification"],
            result.Value.Changes.Select(static change => change.Field));
        Assert.Equal(boundaryId, Assert.Single(result.Value.BoundaryReferences).BoundaryId);
        Assert.Contains("engagements", result.Value.PendingContexts);
        Assert.DoesNotContain("boundary_references_over_limit", result.Value.PendingContexts);
    }

    [Fact]
    public async Task ShouldRejectStaleAndLaggedPreviewGivenAuthoritativeApplication()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var applications = new FitzApplicationDirectory(new InMemoryKvClient());
        var references = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source),
            applications, references, new ApplicationBoundaryReferenceReadConsistency(
                references, new InMemoryEventStore()));
        var request = new PreviewApplicationChange(tenantId, applicationId, 1, "retire");

        // Act
        var lagged = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            request, new ClaimsPrincipal()), CancellationToken.None);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(
            tenantId, applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now));
        var ready = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            request, new ClaimsPrincipal()), CancellationToken.None);
        Assert.True(source.Revise(1, "Payroll v2", "Run payroll", null,
            actorId, "Manager", now).IsSuccess);
        var stale = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            request, new ClaimsPrincipal()), CancellationToken.None);
        var invalid = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            request with { ChangeKind = "activate" }, new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        var lagError = Assert.IsType<RequestError>(lagged.Error);
        Assert.Equal(RequestErrorKind.Conflict, lagError.Kind);
        Assert.True(lagError.IsTransient);
        Assert.True(ready.IsSuccess);
        Assert.Empty(ready.Value.Changes);
        var staleError = Assert.IsType<RequestError>(stale.Error);
        Assert.Equal(RequestErrorKind.Conflict, staleError.Kind);
        Assert.False(staleError.IsTransient);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(invalid.Error).Kind);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenBoundarySourceAheadOfProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        var references = new FitzApplicationBoundaryReferenceDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(
            tenantId, applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now));
        var events = new InMemoryEventStore();
        var stream = new EventStreamAddress(tenantId.ToString(), "boundaries",
            boundaryId.ToString());
        DomainEvent boundary = new BoundaryDraftCreated(tenantId, boundaryId,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            new BoundaryContent("Payroll", "readiness", ["security"],
                [new BoundaryScopeEntry(Uuid.CreateVersion4(), "inclusion",
                    "application", "Payroll", applicationId, "Operations", "In scope", false)]),
            actorId, "Manager", now);
        boundary.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), boundaryId,
            1, now));
        await events.AppendAsync(stream, 0, [boundary]);
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source),
            applications, references, new ApplicationBoundaryReferenceReadConsistency(
                references, events));
        var request = new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal());

        // Act
        var lagged = await handler.HandleAsync(request, CancellationToken.None);
        await using (var cursor = events.ReadAsync(EventStreamPattern.ForPattern(
                         tenantId.ToString(), "boundaries"), EventCursor.Start,
                     CancellationToken.None).GetAsyncEnumerator())
        {
            Assert.True(await cursor.MoveNextAsync());
            await using var batch = await references.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity("ApplicationBoundaryReferencesV1",
                    EventStreamPattern.ForPattern(tenantId.ToString(), "boundaries")),
                ProjectionCheckpoint.Start));
            await references.ApplyAsync(boundary);
            await batch.CommitAsync(new ProjectionCheckpoint(cursor.Current.NextCursor));
        }
        var recovered = await handler.HandleAsync(request, CancellationToken.None);

        // Assert
        var lagError = Assert.IsType<RequestError>(lagged.Error);
        Assert.Equal(RequestErrorKind.Conflict, lagError.Kind);
        Assert.True(lagError.IsTransient);
        Assert.Equal(boundaryId, Assert.Single(recovered.Value.BoundaryReferences).BoundaryId);
    }

    static async Task ProjectApplicationAsync(FitzApplicationDirectory directory,
        Uuid tenantId, DomainEvent domainEvent)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("ApplicationDirectory",
                EventStreamPattern.ForPattern(tenantId.ToString())),
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}
