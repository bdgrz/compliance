using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationControlDraftReferenceDirectoryTests
{
    [Fact]
    public async Task ShouldReplaceAndRemoveCurrentDraftReferencesGivenControlLifecycle()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var directory = new FitzApplicationControlDraftReferenceDirectory(new InMemoryKvClient());
        var identity = new CheckpointIdentity("ApplicationControlDraftReferencesV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "controls"));
        var now = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
        await ApplyAsync(directory, identity, new ControlDraftCreated(tenantId, programId,
            controlId, Uuid.CreateVersion4(), "AC-01", Content(
                Entry("application", applicationId, "Payroll"),
                Entry("system_instance", instanceId, "Payroll production")), actorId,
            "Author", now));

        // Act
        await ApplyAsync(directory, identity, new ControlDraftRevised(tenantId, programId,
            controlId, 2, Content(Entry("system_instance", instanceId,
                "Payroll production")), actorId, "Author", now.AddMinutes(1)));
        var removedApplication = await directory.ListAsync(tenantId, "application",
            applicationId, 20, null);
        var currentInstance = await directory.ListAsync(tenantId, "system_instance",
            instanceId, 20, null);
        await ApplyAsync(directory, identity, new ControlDraftDiscarded(tenantId, programId,
            controlId, 2, actorId, "Author", "Duplicate draft", now.AddMinutes(2)));
        var removedInstance = await directory.ListAsync(tenantId, "system_instance",
            instanceId, 20, null);

        // Assert
        Assert.Empty(removedApplication.Items);
        var reference = Assert.Single(currentInstance.Items);
        Assert.Equal(controlId, reference.ControlId);
        Assert.Equal(programId, reference.ProgramId);
        Assert.Equal(2, reference.Revision);
        Assert.Empty(removedInstance.Items);
    }

    [Fact]
    public async Task ShouldExcludeCurrentReferencesGivenLegacyAndOtherTenantApplicability()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var directory = new FitzApplicationControlDraftReferenceDirectory(new InMemoryKvClient());
        var now = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
        var legacy = new ControlDraftCreated(tenantId, programId, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), "AC-LEGACY", new ControlDraftContent("Legacy control",
                "Preserve legacy history", "A retained legacy control", "Keep it readable.",
                ["Legacy record"]), Uuid.CreateVersion4(), "Author", now);
        var unresolved = new ControlDraftCreated(tenantId, programId, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), "AC-RISK", Content(new ControlApplicabilityReference(
                Uuid.CreateVersion4(), "risk", "Payroll risk", null,
                "Risk scope remains unresolved.", true)), Uuid.CreateVersion4(), "Author", now);
        var otherTenant = new ControlDraftCreated(otherTenantId, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), "AC-OTHER", Content(
                Entry("application", applicationId, "Other payroll")), Uuid.CreateVersion4(),
            "Author", now);

        // Act
        await ApplyAsync(directory, Identity(tenantId), legacy);
        await ApplyAsync(directory, Identity(tenantId), unresolved);
        await ApplyAsync(directory, Identity(otherTenantId), otherTenant);
        var currentTenant = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);
        var otherTenantReferences = await directory.ListAsync(otherTenantId, "application",
            applicationId, 20, null);

        // Assert
        Assert.Empty(currentTenant.Items);
        Assert.Equal(otherTenant.ControlId, Assert.Single(otherTenantReferences.Items).ControlId);
    }

    [Fact]
    public async Task ShouldRollBackReferenceRowsGivenOutOfOrderControlProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var directory = new FitzApplicationControlDraftReferenceDirectory(new InMemoryKvClient());
        var identity = Identity(tenantId);
        DomainEvent created = new ControlDraftCreated(tenantId, Uuid.CreateVersion4(), controlId,
            Uuid.CreateVersion4(), "AC-01", Content(Entry("application", applicationId,
                "Payroll")), Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow);
        DomainEvent outOfOrder = new ControlDraftRevised(tenantId, Uuid.CreateVersion4(), controlId,
            3, Content(), Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow);

        // Act
        await using (var failed = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(outOfOrder));
        }
        var afterFailure = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);
        await ApplyAsync(directory, identity, created);
        var afterRetry = await directory.ListAsync(tenantId, "application", applicationId,
            20, null);

        // Assert
        Assert.Empty(afterFailure.Items);
        Assert.Single(afterRetry.Items);
        Assert.Equal(ProjectionCheckpoint.Start, await directory.LoadCheckpointAsync(tenantId));
    }

    [Fact]
    public async Task ShouldRejectTenantMismatchGivenCurrentControlDraftReferenceState()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var directory = new FitzApplicationControlDraftReferenceDirectory(new InMemoryKvClient());
        var identity = Identity(tenantId);
        DomainEvent created = new ControlDraftCreated(tenantId, programId, controlId,
            Uuid.CreateVersion4(), "AC-01", Content(Entry("application", applicationId,
                "Payroll")), Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow);
        DomainEvent foreignRevision = new ControlDraftRevised(otherTenantId, programId, controlId,
            2, Content(), Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow);
        DomainEvent foreignDiscard = new ControlDraftDiscarded(otherTenantId, programId, controlId,
            1, Uuid.CreateVersion4(), "Author", "Foreign tenant", DateTimeOffset.UtcNow);
        await ApplyAsync(directory, identity, created);

        // Act
        await using (var failed = await directory.BeginAsync(new ProjectionBatchContext(identity,
                         ProjectionCheckpoint.Start)))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(foreignRevision));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(foreignDiscard));
        }
        var current = await directory.ListAsync(tenantId, "application", applicationId, 20, null);

        // Assert
        Assert.Equal(controlId, Assert.Single(current.Items).ControlId);
        Assert.Equal(1, current.Items[0].Revision);
    }

    [Fact]
    public async Task ShouldReportLagThenAllowEmptyReadGivenControlSourceCheckpointCatchup()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var controlId = Uuid.CreateVersion4();
        var directory = new FitzApplicationControlDraftReferenceDirectory(new InMemoryKvClient());
        var events = new InMemoryEventStore();
        var consistency = new ApplicationControlDraftReferenceReadConsistency(directory, events);
        var stream = new EventStreamAddress(tenantId.ToString(), "controls", controlId.ToString());
        DomainEvent created = new ControlDraftCreated(tenantId, Uuid.CreateVersion4(), controlId,
            Uuid.CreateVersion4(), "AC-01", Content(), Uuid.CreateVersion4(), "Author",
            DateTimeOffset.UtcNow);
        created.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), controlId, 1,
            DateTimeOffset.UtcNow));

        // Act
        var noSource = await consistency.EnsureCaughtUpAsync(tenantId, CancellationToken.None);
        await events.AppendAsync(stream, 0, [created]);
        var lagged = await consistency.EnsureCaughtUpAsync(tenantId, CancellationToken.None);
        await using var source = events.ReadAsync(EventStreamPattern.ForPattern(
            tenantId.ToString(), "controls"), EventCursor.Start, CancellationToken.None)
            .GetAsyncEnumerator();
        Assert.True(await source.MoveNextAsync());
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         Identity(tenantId), ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(created);
            await batch.CommitAsync(new ProjectionCheckpoint(source.Current.NextCursor));
        }
        var caughtUp = await consistency.EnsureCaughtUpAsync(tenantId, CancellationToken.None);

        // Assert
        Assert.True(noSource.IsSuccess);
        Assert.False(lagged.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, lagged.Error.Kind);
        Assert.True(lagged.Error.IsTransient);
        Assert.True(caughtUp.IsSuccess);
        Assert.Empty((await directory.ListAsync(tenantId, "application", Uuid.CreateVersion4(),
            20, null)).Items);
    }

    static ControlDraftContent Content(params ControlApplicabilityReference[] applicability) =>
        new("Access controls", "Restrict payroll access", "Payroll access control",
            "Administrators use approved access paths.", ["Access review log"], null, applicability);

    static ControlApplicabilityReference Entry(string subjectType, Uuid recordId, string subject) =>
        new(Uuid.CreateVersion4(), subjectType, subject, recordId,
            "The control applies to this governed record.", false);

    static CheckpointIdentity Identity(Uuid tenantId) =>
        new("ApplicationControlDraftReferencesV1",
            EventStreamPattern.ForPattern(tenantId.ToString(), "controls"));

    static async Task ApplyAsync(FitzApplicationControlDraftReferenceDirectory directory,
        CheckpointIdentity identity, DomainEvent domainEvent)
    {
        await using var batch = await directory.BeginAsync(
            new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
