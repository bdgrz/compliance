using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Fitz.Testing;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FindingClosureWorkTests
{
    [Fact]
    public async Task ShouldReconcileAssignedClosureCountsActionsAndNotificationsGivenSourceCompletion()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await ReadyAsync(fixture, fixture.OwnerUserId);
        finding = await fixture.AsAsync(fixture.LeadUserId, new ReviseFinding(fixture.TenantId,
            fixture.ProgramId, finding.FindingId, finding.Revision, "high", fixture.LeadMemberId,
            fixture.Today.AddDays(-7), "VPN", null, "Verification deadline passed."));
        var queue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var item = Assert.Single(queue.Items);
        await fixture.AsAsync(fixture.ApproverUserId, new AssignWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 0, fixture.ApproverMemberId));

        // Act
        var mine = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        var detail = await fixture.AsAsync(fixture.ApproverUserId,
            new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId));
        var reminders = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWorkReminders(fixture.TenantId, fixture.ProgramId));
        var digest = await fixture.AsAsync(fixture.ApproverUserId,
            new GetWorkDigest(fixture.TenantId, fixture.ProgramId));
        var closed = await fixture.AsAsync(fixture.ApproverUserId, new CloseFinding(
            fixture.TenantId, fixture.ProgramId, finding.FindingId, finding.Revision,
            "Independently verified correction.", OperationsFixture.FullSupport, "Closure accepted."));
        var after = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "all"));
        var afterReminders = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWorkReminders(fixture.TenantId, fixture.ProgramId));
        var afterDigest = await fixture.AsAsync(fixture.ApproverUserId,
            new GetWorkDigest(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Equal(new WorkCountsView(1, 1, 0, 1), mine.Counts);
        Assert.Equal(Assert.Single(mine.Items), detail.Item);
        Assert.Equal(item.WorkItemId, Assert.Single(reminders).WorkItemId);
        Assert.Equal(item.WorkItemId, Assert.Single(digest.Overdue).WorkItemId);
        Assert.Equal(RemediationLedger.Closed, closed.Status);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), after.Counts);
        Assert.Empty(afterReminders);
        Assert.Empty(afterDigest.Overdue);
    }

    [Theory]
    [InlineData("finding_owner")]
    [InlineData("action_owner")]
    [InlineData("completer")]
    [InlineData("no_management")]
    public async Task ShouldOmitClosureCountsAndActionsGivenIneligibleActor(string role)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await ReadyAsync(fixture,
            role == "completer" ? fixture.ApproverUserId : fixture.OwnerUserId);
        var actor = role switch
        {
            "finding_owner" => fixture.LeadUserId,
            "completer" => fixture.ApproverUserId,
            "no_management" => fixture.OutsiderUserId,
            _ => fixture.OwnerUserId,
        };
        var candidate = FindingClosureWork.Candidate(finding)!;

        // Act
        var queue = await fixture.AsAsync(actor, new ListWork(fixture.TenantId,
            fixture.ProgramId, "all"));
        var detail = await fixture.Scenario(actor).When(new GetWorkItem(fixture.TenantId,
            fixture.ProgramId, candidate.WorkItemId)).ExpectFailure(RequestErrorKind.NotFound);
        var reminders = await fixture.AsAsync(actor,
            new ListWorkReminders(fixture.TenantId, fixture.ProgramId));
        var digest = await fixture.AsAsync(actor,
            new GetWorkDigest(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), queue.Counts);
        Assert.Empty(queue.Items);
        Assert.False(detail.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, detail.Error.Kind);
        Assert.Empty(reminders);
        Assert.Empty(digest.DueSoon);
        Assert.Empty(digest.Overdue);
    }

    [Fact]
    public async Task ShouldDropPreviousAssignmentGivenFindingRevisionChange()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await ReadyAsync(fixture, fixture.OwnerUserId);
        var candidate = FindingClosureWork.Candidate(finding)!;
        await fixture.AsAsync(fixture.ApproverUserId, new AssignWorkItem(fixture.TenantId,
            fixture.ProgramId, candidate.WorkItemId, 0, fixture.ApproverMemberId));

        // Act
        finding = await fixture.AsAsync(fixture.LeadUserId, new ReviseFinding(fixture.TenantId,
            fixture.ProgramId, finding.FindingId, finding.Revision, "medium", fixture.LeadMemberId,
            finding.DueOn, "VPN", null, "Severity reassessed."));
        var queue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var previous = await fixture.Scenario(fixture.ApproverUserId).When(
            new GetWorkItem(fixture.TenantId, fixture.ProgramId, candidate.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        var item = Assert.Single(queue.Items);
        Assert.NotEqual(candidate.WorkItemId, item.WorkItemId);
        Assert.Equal(FindingClosureWork.Candidate(finding)!.WorkItemId, item.WorkItemId);
        Assert.Null(item.AssigneeMemberId);
        Assert.False(previous.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, previous.Error.Kind);
    }

    [Fact]
    public async Task ShouldRejectAssignmentGivenSourceInvolvedMember()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await ReadyAsync(fixture, fixture.OwnerUserId);
        var candidate = FindingClosureWork.Candidate(finding)!;

        // Act
        var assigned = await fixture.Scenario(fixture.ApproverUserId).When(
            new AssignWorkItem(fixture.TenantId, fixture.ProgramId, candidate.WorkItemId,
                0, fixture.OwnerMemberId)).ExpectFailure(RequestErrorKind.Validation);

        // Assert
        Assert.False(assigned.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, assigned.Error.Kind);
    }

    [Fact]
    public async Task ShouldFenceClosureCountsGivenLaggingOrChangingSourceProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await ReadyAsync(fixture, fixture.OwnerUserId);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient());
        var consistency = new WorkQueueReadConsistency(events, [directory]);
        var lagging = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);
        var pattern = directory.SourcePattern(fixture.TenantId);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity(FitzFindingClosureWorkItemDirectory.ProjectorName,
                             pattern), ProjectionCheckpoint.Start)))
        {
            var cursor = ProjectionCheckpoint.Start.Cursor;
            await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
            {
                await directory.ApplyAsync(record.Event);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [directory]);

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId,
            new OperationsActor(fixture.ApproverUserId, fixture.ApproverMemberId, "Reviewer"),
            30, CancellationToken.None);
        await fixture.AsAsync(fixture.LeadUserId, new ReviseFinding(fixture.TenantId,
            fixture.ProgramId, finding.FindingId, finding.Revision, "medium", fixture.LeadMemberId,
            finding.DueOn, "VPN", null, "Review inputs changed."));
        var changed = await consistency.ConfirmUnchangedAndCaughtUpAsync(fixture.TenantId,
            captured.Value, CancellationToken.None);

        // Assert
        Assert.False(lagging.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, lagging.Error.Kind);
        Assert.True(lagging.Error.IsTransient);
        Assert.True(captured.IsSuccess);
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries);
        Assert.Equal(FindingClosureWork.Kind, entry.Item.Kind);
        Assert.Equal(finding.FindingId, entry.Item.SourceId);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error.Kind);
        Assert.True(changed.Error.IsTransient);
    }

    static async Task<FindingView> ReadyAsync(OperationsFixture fixture, Uuid completerUserId)
    {
        var finding = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today);
        return await fixture.AsAsync(completerUserId, new CompleteCorrectiveAction(fixture.TenantId,
            fixture.ProgramId, finding.FindingId, finding.Revision,
            Assert.Single(finding.CorrectiveActions).ActionId, "Removed obsolete access.",
            OperationsFixture.FullSupport));
    }
}
