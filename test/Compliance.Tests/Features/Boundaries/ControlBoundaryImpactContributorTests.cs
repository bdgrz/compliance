using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Boundaries;

public sealed class ControlBoundaryImpactContributorTests
{
    [Fact]
    public async Task ShouldReportCurrentDraftsAndRemainIncompleteGivenChangedGovernedScope()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var applicationEntryId = Uuid.CreateVersion4();
        var systemInstanceEntryId = Uuid.CreateVersion4();
        var draftId = Uuid.CreateVersion4();
        var approvedVersionId = Uuid.CreateVersion4();
        var firstControlId = Uuid.CreateVersion4();
        var secondControlId = Uuid.CreateVersion4();
        var otherProgramControlId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 22, 17, 30, 0, TimeSpan.Zero);
        var previousApplication = ResolvedEntry(applicationEntryId, "application", applicationId);
        var proposedSystemInstance = ResolvedEntry(systemInstanceEntryId, "system_instance",
            systemInstanceId);
        var references = new ControlReferenceDirectory
        {
            Pages =
            {
                [("application", applicationId)] = new Page<ApplicationControlDraftReferenceView>([
                    Reference(tenantId, applicationId, programId, firstControlId,
                        Uuid.CreateVersion4()),
                    Reference(tenantId, applicationId, programId, firstControlId,
                        Uuid.CreateVersion4()),
                    Reference(tenantId, applicationId, Uuid.CreateVersion4(),
                        otherProgramControlId, Uuid.CreateVersion4()),
                ], null),
                [("system_instance", systemInstanceId)] = new Page<ApplicationControlDraftReferenceView>([
                    Reference(tenantId, systemInstanceId, programId, secondControlId,
                        Uuid.CreateVersion4(), "system_instance"),
                ], null),
            },
        };
        var contributor = new ControlBoundaryImpactContributor(references,
            new ApplicationControlDraftReferenceReadConsistency(references,
                new InMemoryEventStore()));
        var previous = new BoundaryVersionView(tenantId, boundaryId, programId, approvedVersionId,
            1, new BoundaryContent("Original", "readiness", ["security"],
                [previousApplication]), "approved", new DateOnly(2026, 1, 1),
            Uuid.CreateVersion4(), "Author", now);
        var proposed = new BoundaryVersionView(tenantId, boundaryId, programId, draftId, 1,
            new BoundaryContent("Successor", "readiness", ["security"],
                [proposedSystemInstance]), "draft", null, Uuid.CreateVersion4(), "Author", now);
        var boundary = new BoundaryView(tenantId, boundaryId, programId, proposed, previous,
            null, 2);
        IReadOnlyList<BoundaryChange> changes =
        [
            new BoundaryChange("scope_entry", "removed", applicationEntryId, null, null,
                previousApplication, null),
            new BoundaryChange("scope_entry", "added", systemInstanceEntryId, null, null,
                null, proposedSystemInstance),
        ];

        // Act
        var result = await contributor.ContributeAsync(boundary, changes, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("controls", result.Value.Context);
        Assert.False(result.Value.Complete);
        Uuid[] expectedControlIds = [firstControlId, secondControlId];
        Assert.Equal(expectedControlIds.OrderBy(static id => id.ToString(), StringComparer.Ordinal),
            result.Value.Records.Select(static record => record.RecordId));
        Assert.All(result.Value.Records, record =>
        {
            Assert.Equal(tenantId, record.TenantId);
            Assert.Equal("controls", record.Context);
            Assert.Equal("control_draft", record.RecordType);
        });
        Assert.DoesNotContain(result.Value.Records, record =>
            record.RecordId == otherProgramControlId);
        Assert.Collection(references.Requests,
            request =>
            {
                Assert.Equal(("application", applicationId),
                    (request.SubjectType, request.RecordId));
                Assert.Equal(200, request.Limit);
            },
            request =>
            {
                Assert.Equal(("system_instance", systemInstanceId),
                    (request.SubjectType, request.RecordId));
                Assert.Equal(197, request.Limit);
            });
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenControlSourceChangesDuringImpactRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 22, 17, 30, 0, TimeSpan.Zero);
        var previousApplication = ResolvedEntry(Uuid.CreateVersion4(), "application", applicationId);
        var events = new InMemoryEventStore();
        DomainEvent arriving = new ControlDraftCreated(tenantId, programId, controlId,
            Uuid.CreateVersion4(), "AC-01", new ControlDraftContent("Access control",
                "Restrict payroll access", "Payroll access control",
                "Use approved access paths.", ["Access review log"]), Uuid.CreateVersion4(),
            "Manager", now);
        arriving.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), controlId, 1, now));
        var references = new ControlReferenceDirectory
        {
            Pages =
            {
                [("application", applicationId)] = new Page<ApplicationControlDraftReferenceView>([
                    Reference(tenantId, applicationId, programId, controlId,
                        Uuid.CreateVersion4()),
                ], null),
            },
            OnListAsync = ct => events.AppendAsync(new EventStreamAddress(tenantId.ToString(),
                "controls", controlId.ToString()), 0, [arriving], ct),
        };
        var contributor = new ControlBoundaryImpactContributor(references,
            new ApplicationControlDraftReferenceReadConsistency(references, events));
        var approved = new BoundaryVersionView(tenantId, boundaryId, programId,
            Uuid.CreateVersion4(), 1, new BoundaryContent("Original", "readiness", ["security"],
                [previousApplication]), "approved", new DateOnly(2026, 1, 1),
            Uuid.CreateVersion4(), "Author", now);
        var draft = new BoundaryVersionView(tenantId, boundaryId, programId,
            Uuid.CreateVersion4(), 1, new BoundaryContent("Successor", "readiness", ["security"],
                []), "draft", null, Uuid.CreateVersion4(), "Author", now);
        var boundary = new BoundaryView(tenantId, boundaryId, programId, draft, approved, null, 2);
        var impact = new BoundaryImpactService(new BoundaryDirectory(boundary),
            [new ProgramBoundaryImpactContributor(), contributor]);

        // Act
        var result = await impact.PreviewAsync(new PreviewBoundaryImpact(tenantId, boundaryId,
            draft.VersionId, draft.Revision), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenControlProjectionCatchesUpDuringImpactRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var arrivingControlId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 22, 17, 30, 0, TimeSpan.Zero);
        var previousApplication = ResolvedEntry(Uuid.CreateVersion4(), "application", applicationId);
        var events = new InMemoryEventStore();
        DomainEvent arriving = new ControlDraftCreated(tenantId, programId, arrivingControlId,
            Uuid.CreateVersion4(), "AC-01", new ControlDraftContent("Access control",
                "Restrict payroll access", "Payroll access control",
                "Use approved access paths.", ["Access review log"]), Uuid.CreateVersion4(),
            "Manager", now);
        arriving.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), arrivingControlId, 1,
            now));
        var references = new ControlReferenceDirectory
        {
            Pages =
            {
                [("application", applicationId)] = new Page<ApplicationControlDraftReferenceView>([
                    Reference(tenantId, applicationId, programId, controlId,
                        Uuid.CreateVersion4()),
                ], null),
            },
        };
        references.OnListAsync = async ct =>
        {
            await events.AppendAsync(new EventStreamAddress(tenantId.ToString(), "controls",
                arrivingControlId.ToString()), 0, [arriving], ct);
            var current = references.Pages[("application", applicationId)];
            references.Pages[("application", applicationId)] = new Page<
                ApplicationControlDraftReferenceView>([
                .. current.Items,
                Reference(tenantId, applicationId, programId, arrivingControlId,
                    Uuid.CreateVersion4()),
            ], null);
            references.Checkpoint = await CheckpointAfterAsync(events, tenantId, ct);
        };
        var contributor = new ControlBoundaryImpactContributor(references,
            new ApplicationControlDraftReferenceReadConsistency(references, events));
        var approved = new BoundaryVersionView(tenantId, boundaryId, programId,
            Uuid.CreateVersion4(), 1, new BoundaryContent("Original", "readiness", ["security"],
                [previousApplication]), "approved", new DateOnly(2026, 1, 1),
            Uuid.CreateVersion4(), "Author", now);
        var draft = new BoundaryVersionView(tenantId, boundaryId, programId,
            Uuid.CreateVersion4(), 1, new BoundaryContent("Successor", "readiness", ["security"],
                []), "draft", null, Uuid.CreateVersion4(), "Author", now);
        var boundary = new BoundaryView(tenantId, boundaryId, programId, draft, approved, null, 2);
        var impact = new BoundaryImpactService(new BoundaryDirectory(boundary),
            [new ProgramBoundaryImpactContributor(), contributor]);

        // Act
        var result = await impact.PreviewAsync(new PreviewBoundaryImpact(tenantId, boundaryId,
            draft.VersionId, draft.Revision), CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenMismatchedControlReferenceProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var previousApplication = ResolvedEntry(Uuid.CreateVersion4(), "application", applicationId);
        var now = new DateTimeOffset(2026, 9, 22, 17, 30, 0, TimeSpan.Zero);
        var references = new ControlReferenceDirectory
        {
            Pages =
            {
                [("application", applicationId)] = new Page<ApplicationControlDraftReferenceView>([
                    Reference(Uuid.CreateVersion4(), applicationId, programId,
                        Uuid.CreateVersion4(), Uuid.CreateVersion4()),
                ], null),
            },
        };
        var contributor = new ControlBoundaryImpactContributor(references,
            new ApplicationControlDraftReferenceReadConsistency(references,
                new InMemoryEventStore()));
        var approved = new BoundaryVersionView(tenantId, boundaryId, programId,
            Uuid.CreateVersion4(), 1, new BoundaryContent("Original", "readiness", ["security"],
                [previousApplication]), "approved", new DateOnly(2026, 1, 1),
            Uuid.CreateVersion4(), "Author", now);
        var draft = new BoundaryVersionView(tenantId, boundaryId, programId,
            Uuid.CreateVersion4(), 1, new BoundaryContent("Successor", "readiness", ["security"],
                []), "draft", null, Uuid.CreateVersion4(), "Author", now);
        var boundary = new BoundaryView(tenantId, boundaryId, programId, draft, approved, null, 2);
        IReadOnlyList<BoundaryChange> changes =
        [
            new BoundaryChange("scope_entry", "removed", previousApplication.EntryId, null, null,
                previousApplication, null),
        ];

        // Act
        var result = await contributor.ContributeAsync(boundary, changes, CancellationToken.None);

        // Assert
        var error = Assert.IsType<RequestError>(result.Error);
        Assert.Equal(RequestErrorKind.Conflict, error.Kind);
        Assert.True(error.IsTransient);
    }

    static BoundaryScopeEntry ResolvedEntry(Uuid entryId, string subjectType, Uuid recordId) =>
        new(entryId, "inclusion", subjectType, subjectType, recordId, "Owner", "Scope", false);

    static ApplicationControlDraftReferenceView Reference(Uuid tenantId, Uuid recordId,
        Uuid programId, Uuid controlId, Uuid entryId, string subjectType = "application") =>
        new(tenantId, subjectType, recordId, programId, controlId, "AC-01", 1, entryId,
            subjectType, "The current draft applies to this scope.");

    static async Task<ProjectionCheckpoint> CheckpointAfterAsync(InMemoryEventStore events,
        Uuid tenantId, CancellationToken ct)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           tenantId.ToString(), "controls"), cursor, ct))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed class ControlReferenceDirectory : IApplicationControlDraftReferenceDirectory
    {
        public Dictionary<(string SubjectType, Uuid RecordId),
            Page<ApplicationControlDraftReferenceView>> Pages
        { get; } = [];

        public List<(string SubjectType, Uuid RecordId, int Limit, string? Cursor)> Requests
        {
            get;
        } = [];

        public ProjectionCheckpoint Checkpoint { get; set; } = ProjectionCheckpoint.Start;
        public Func<CancellationToken, ValueTask>? OnListAsync { get; set; }

        public async ValueTask<Page<ApplicationControlDraftReferenceView>> ListAsync(Uuid tenantId,
            string subjectType, Uuid recordId, int limit, string? cursor,
            CancellationToken ct = default)
        {
            Requests.Add((subjectType, recordId, limit, cursor));
            var page = Pages.GetValueOrDefault((subjectType, recordId),
                new Page<ApplicationControlDraftReferenceView>([], null));
            if (OnListAsync is not null)
                await OnListAsync(ct);
            return page;
        }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(Checkpoint);
    }

    sealed class BoundaryDirectory(BoundaryView current) : IBoundaryDirectoryReader
    {
        public ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
            CancellationToken ct = default) => ValueTask.FromResult<BoundaryView?>(current);

        public ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid versionId, CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
            Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId,
            Uuid boundaryId, Uuid decisionId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => throw new NotSupportedException();
    }
}
