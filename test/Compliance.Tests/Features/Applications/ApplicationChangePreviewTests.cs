using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationChangePreviewTests
{
    [Fact]
    public async Task ShouldReportCurrentDraftControlReferencesGivenApplicationPreview()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var applicationEntryId = Uuid.CreateVersion4();
        var systemInstanceEntryId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv);
        var events = new InMemoryEventStore();
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now));
        DomainEvent control = new ControlDraftCreated(tenantId, programId, controlId,
            Uuid.CreateVersion4(), "AC-01", new ControlDraftContent("Access control",
                "Restrict payroll access", "Payroll access control",
                "Use approved access paths.", ["Access review log"], null,
                [new ControlApplicabilityReference(applicationEntryId, "application", "Payroll",
                    applicationId, "The control applies to payroll.", false),
                new ControlApplicabilityReference(systemInstanceEntryId, "system_instance",
                    "Payroll production", systemInstanceId,
                    "The control applies to payroll production.", false)]),
            actorId, "Manager", now);
        control.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), controlId, 1, now));
        await events.AppendAsync(new EventStreamAddress(tenantId.ToString(), "controls",
            controlId.ToString()), 0, [control]);
        await using (var cursor = events.ReadAsync(EventStreamPattern.ForPattern(
                         tenantId.ToString(), "controls"), EventCursor.Start,
                     CancellationToken.None).GetAsyncEnumerator())
        {
            Assert.True(await cursor.MoveNextAsync());
            await using var batch = await controls.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity("ApplicationControlDraftReferencesV1",
                    EventStreamPattern.ForPattern(tenantId.ToString(), "controls")),
                ProjectionCheckpoint.Start));
            await controls.ApplyAsync(control);
            await batch.CommitAsync(new ProjectionCheckpoint(cursor.Current.NextCursor));
        }
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source), applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var reference = Assert.Single(result.Value.ControlDraftReferences);
        Assert.Equal(controlId, reference.ControlId);
        Assert.Equal(programId, reference.ProgramId);
        Assert.Equal("AC-01", reference.Identifier);
        Assert.Equal(applicationEntryId, reference.EntryId);
        Assert.DoesNotContain(result.Value.ControlDraftReferences,
            item => item.SubjectType == "system_instance");
        Assert.Contains("approved_control_versions_and_lifecycle_impact",
            result.Value.PendingContexts);
        Assert.Contains("system_instance_control_draft_references", result.Value.PendingContexts);
        Assert.DoesNotContain("controls", result.Value.PendingContexts);
        Assert.False(result.Value.Complete);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenControlSourceAheadOfProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var kv = new InMemoryKvClient();
        var applications = new FitzApplicationDirectory(kv);
        var references = new FitzApplicationBoundaryReferenceDirectory(kv);
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv);
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now));
        var events = new InMemoryEventStore();
        var stream = new EventStreamAddress(tenantId.ToString(), "controls", controlId.ToString());
        DomainEvent control = new ControlDraftCreated(tenantId, programId, controlId,
            Uuid.CreateVersion4(), "AC-01", new ControlDraftContent("Access control",
                "Restrict payroll access", "Payroll access control",
                "Use approved access paths.", ["Access review log"], null,
                [new ControlApplicabilityReference(Uuid.CreateVersion4(), "application",
                    "Payroll", applicationId, "The control applies to payroll.", false)]),
            actorId, "Manager", now);
        control.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), controlId, 1, now));
        await events.AppendAsync(stream, 0, [control]);
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source), applications,
            references, new ApplicationBoundaryReferenceReadConsistency(references, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events));
        var request = new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal());

        // Act
        var lagged = await handler.HandleAsync(request, CancellationToken.None);
        await using (var cursor = events.ReadAsync(EventStreamPattern.ForPattern(
                         tenantId.ToString(), "controls"), EventCursor.Start,
                     CancellationToken.None).GetAsyncEnumerator())
        {
            Assert.True(await cursor.MoveNextAsync());
            await using var batch = await controls.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity("ApplicationControlDraftReferencesV1",
                    EventStreamPattern.ForPattern(tenantId.ToString(), "controls")),
                ProjectionCheckpoint.Start));
            await controls.ApplyAsync(control);
            await batch.CommitAsync(new ProjectionCheckpoint(cursor.Current.NextCursor));
        }
        var recovered = await handler.HandleAsync(request, CancellationToken.None);

        // Assert
        var lagError = Assert.IsType<RequestError>(lagged.Error);
        Assert.Equal(RequestErrorKind.Conflict, lagError.Kind);
        Assert.True(lagError.IsTransient);
        Assert.True(recovered.IsSuccess);
        Assert.Equal(controlId, Assert.Single(recovered.Value.ControlDraftReferences).ControlId);
    }

    [Fact]
    public async Task ShouldMarkControlDraftReferencesOverLimitGivenTruncatedPreview()
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
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now));
        var controls = new ControlReferenceDirectory
        {
            Page = new Page<ApplicationControlDraftReferenceView>([
                ControlReference(tenantId, applicationId),
            ], "more"),
        };
        var events = new InMemoryEventStore();
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source), applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events),
            controls, new ApplicationControlDraftReferenceReadConsistency(controls, events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, controls.Limit);
        Assert.Contains("control_draft_references_over_limit", result.Value.PendingContexts);
        Assert.Single(result.Value.ControlDraftReferences);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenMismatchedControlReferenceProjection()
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
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now));
        var controls = new ControlReferenceDirectory
        {
            Page = new Page<ApplicationControlDraftReferenceView>([
                ControlReference(Uuid.CreateVersion4(), applicationId),
            ], null),
        };
        var events = new InMemoryEventStore();
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source), applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenControlSourceChangesDuringPreviewRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null, actorId, "Manager", now)
            .IsSuccess);
        var applications = new FitzApplicationDirectory(new InMemoryKvClient());
        await ProjectApplicationAsync(applications, tenantId, new ApplicationDeclared(tenantId,
            applicationId, "Payroll", "Run payroll", null, actorId, "Manager", now));
        var events = new InMemoryEventStore();
        DomainEvent arriving = new ControlDraftCreated(tenantId, Uuid.CreateVersion4(), controlId,
            Uuid.CreateVersion4(), "AC-01", new ControlDraftContent("Access control",
                "Restrict payroll access", "Payroll access control",
                "Use approved access paths.", ["Access review log"]), actorId, "Manager", now);
        arriving.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), controlId, 1, now));
        var controls = new ControlReferenceDirectory
        {
            Page = new Page<ApplicationControlDraftReferenceView>([
                ControlReference(tenantId, applicationId),
            ], null),
            OnListAsync = ct => events.AppendAsync(new EventStreamAddress(tenantId.ToString(),
                "controls", controlId.ToString()), 0, [arriving], ct),
        };
        var boundaries = new FitzApplicationBoundaryReferenceDirectory(new InMemoryKvClient());
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source), applications,
            boundaries, new ApplicationBoundaryReferenceReadConsistency(boundaries, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events));

        // Act
        var result = await handler.HandleAsync(new RequestContext<PreviewApplicationChange>(
            new PreviewApplicationChange(tenantId, applicationId, 1, "retire"),
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

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
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv);
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
                references, new InMemoryEventStore()), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, new InMemoryEventStore()));

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
        Assert.Contains("approved_control_versions_and_lifecycle_impact",
            result.Value.PendingContexts);
        Assert.DoesNotContain("controls", result.Value.PendingContexts);
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
        var controls = new FitzApplicationControlDraftReferenceDirectory(new InMemoryKvClient());
        var events = new InMemoryEventStore();
        var handler = new PreviewApplicationChangeHandler(new SourceReader(source),
            applications, references, new ApplicationBoundaryReferenceReadConsistency(
                references, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events));
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
        var controls = new FitzApplicationControlDraftReferenceDirectory(kv);
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
                references, events), controls,
            new ApplicationControlDraftReferenceReadConsistency(controls, events));
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
            new CheckpointIdentity("ApplicationDirectoryV2",
                EventStreamPattern.ForPattern(tenantId.ToString())),
            ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static ApplicationControlDraftReferenceView ControlReference(Uuid tenantId,
        Uuid applicationId) => new(tenantId, "application", applicationId,
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), "AC-01", 1, Uuid.CreateVersion4(),
        "Payroll", "The draft applies to payroll.");

    sealed class ControlReferenceDirectory : IApplicationControlDraftReferenceDirectory
    {
        public int Limit { get; private set; }
        public Page<ApplicationControlDraftReferenceView> Page { get; init; } = new([], null);
        public Func<CancellationToken, ValueTask>? OnListAsync { get; init; }

        public async ValueTask<Page<ApplicationControlDraftReferenceView>> ListAsync(Uuid tenantId,
            string subjectType, Uuid recordId, int limit, string? cursor,
            CancellationToken ct = default)
        {
            Limit = limit;
            if (OnListAsync is not null)
                await OnListAsync(ct);
            return Page;
        }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);
    }

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}
