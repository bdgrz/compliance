using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzPolicyDecisionWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldProjectPendingReviewWithCurrentSourceCandidateGivenPolicyDraftCreated()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var policyId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, policyId, 12);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzPolicyDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>(), TimeProvider.System);
        var createdAt = At(fixture.Today);
        var created = new PolicyDraftCreated(fixture.TenantId, fixture.ProgramId, policyId,
            Uuid.CreateVersion4(), "POL-WORK", Content(12), "content-hash",
            Actor(fixture.LeadMemberId), fixture.LeadMemberId, createdAt);

        // Act
        await ProjectAsync(directory, events, fixture.TenantId, created);
        var work = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            fixture.Today, fixture.Today.AddDays(30), null);
        var policy = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new Policy(fixture.TenantId, policyId));
        var expected = PolicyDecisionWork.Candidates(policy, policy.ToView(fixture.Today)!,
            fixture.TenantId, fixture.ProgramId, DateOnly.MaxValue, null);

        // Assert
        Assert.True(work.IsSuccess);
        var item = Assert.Single(work.Value);
        var expectedItem = Assert.Single(expected);
        Assert.Equal(expectedItem.WorkItemId, item.WorkItemId);
        Assert.Equal("policy_draft_review", item.Kind);
        Assert.Equal(expectedItem.ActionPath, item.ActionPath);
        Assert.Equal(fixture.LeadMemberId, Assert.Single(item.Excluded));
        Assert.Equal(1, await directory.LoadRevisionAsync(fixture.TenantId));
        Assert.Equal(await ReadPolicyCheckpointAsync(events, fixture.TenantId),
            await directory.LoadCheckpointAsync(fixture.TenantId));
        Assert.Empty((await directory.LoadProgramAsync(fixture.TenantId, Uuid.CreateVersion4(),
            fixture.Today, fixture.Today.AddDays(30), null)).Value);
        Assert.Empty((await directory.LoadProgramAsync(Uuid.CreateVersion4(), fixture.ProgramId,
            fixture.Today, fixture.Today.AddDays(30), null)).Value);
    }

    [Fact]
    public async Task ShouldAdvanceFromReviewToApprovalAndKeepNextCycleBeyondHorizonGivenPolicyDecisions()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var policyId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, policyId, 1);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzPolicyDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>(), TimeProvider.System);
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyDraftCreated(fixture.TenantId, fixture.ProgramId, policyId,
                Uuid.CreateVersion4(), "POL-WORK", Content(1), "content-hash",
                Actor(fixture.LeadMemberId), fixture.LeadMemberId, At(fixture.Today)));

        var reviewId = Uuid.CreateVersion4();
        var reviewAt = At(fixture.Today).AddMinutes(1);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Policy(fixture.TenantId,
            policyId), policy =>
        {
            Assert.Null(policy.Review(fixture.ProgramId, 1, reviewId, "accept",
                "The policy draft is ready.", Actor(fixture.ReviewerMemberId),
                fixture.ReviewerMemberId, reviewAt));
            return Result.Success;
        });

        // Act
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyReviewed(fixture.TenantId, fixture.ProgramId, policyId, 1, reviewId,
                "accept", "The policy draft is ready.", Actor(fixture.ReviewerMemberId),
                fixture.ReviewerMemberId, reviewAt, null));
        var approval = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, fixture.Today, fixture.Today.AddDays(30), null)).Value);
        var approvalId = Uuid.CreateVersion4();
        var approvedOn = fixture.Today.AddMonths(-1);
        var approvedAt = At(approvedOn).AddMinutes(2);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Policy(fixture.TenantId,
            policyId), policy =>
        {
            Assert.Null(policy.Approve(fixture.ProgramId, 1, approvalId, reviewId,
                approvedOn, true, "Approve the policy.", null,
                Actor(fixture.ApproverMemberId), fixture.ApproverMemberId, approvedAt));
            return Result.Success;
        });
        var approved = new PolicyApproved(fixture.TenantId, fixture.ProgramId, policyId, 1, 1,
            true, approvalId, reviewId, "content-hash", approvedOn, null, null,
            "Approve the policy.", Actor(fixture.ApproverMemberId), fixture.ApproverMemberId,
            approvedAt, null);

        // Assert
        Assert.Equal("policy_draft_approval", approval.Kind);
        await ProjectAsync(directory, events, fixture.TenantId, approved);
        var dueOn = approvedOn.AddMonths(1);
        var beforeHorizon = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            fixture.Today, dueOn.AddDays(-1), null);
        var withinHorizon = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            fixture.Today, dueOn, null);
        var periodic = Assert.Single(withinHorizon.Value);
        Assert.True(beforeHorizon.IsSuccess);
        Assert.Empty(beforeHorizon.Value);
        Assert.Equal("policy_periodic_review", periodic.Kind);
        Assert.Equal(dueOn, periodic.DueOn);
        Assert.NotEqual(approval.WorkItemId, periodic.WorkItemId);
        Assert.Equal(3, await directory.LoadRevisionAsync(fixture.TenantId));

        var periodicReviewId = Uuid.CreateVersion4();
        var periodicReviewedAt = At(dueOn);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Policy(fixture.TenantId,
            policyId), policy =>
        {
            Assert.Null(policy.ConfirmPeriodicReview(fixture.ProgramId, 1, periodicReviewId,
                "The current version remains appropriate.", Actor(fixture.ApproverMemberId),
                fixture.ApproverMemberId, periodicReviewedAt));
            return Result.Success;
        });
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyPeriodicReviewConfirmed(fixture.TenantId, fixture.ProgramId, policyId, 1,
                periodicReviewId, dueOn, "The current version remains appropriate.",
                Actor(fixture.ApproverMemberId), fixture.ApproverMemberId, periodicReviewedAt));
        var nextDueOn = dueOn.AddMonths(1);
        var oldCycle = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            fixture.Today, dueOn, null);
        var nextCycle = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            fixture.Today, nextDueOn, null);
        Assert.True(oldCycle.IsSuccess);
        Assert.Empty(oldCycle.Value);
        var next = Assert.Single(nextCycle.Value);
        Assert.Equal(nextDueOn, next.DueOn);
        Assert.NotEqual(periodic.WorkItemId, next.WorkItemId);
        Assert.Equal(4, await directory.LoadRevisionAsync(fixture.TenantId));
    }

    [Fact]
    public async Task ShouldProjectRetirementReviewAndApprovalThenRemoveWorkGivenRetirementLifecycle()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var policyId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, policyId, 12);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzPolicyDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>(), TimeProvider.System);
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyDraftCreated(fixture.TenantId, fixture.ProgramId, policyId,
                Uuid.CreateVersion4(), "POL-WORK", Content(12), "content-hash",
                Actor(fixture.LeadMemberId), fixture.LeadMemberId, At(fixture.Today)));

        var reviewId = Uuid.CreateVersion4();
        var approvalId = Uuid.CreateVersion4();
        var approvedOn = fixture.Today.AddDays(-40);
        var approvedAt = At(approvedOn).AddMinutes(2);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Policy(fixture.TenantId,
            policyId), policy =>
        {
            Assert.Null(policy.Review(fixture.ProgramId, 1, reviewId, "accept",
                "The policy draft is ready.", Actor(fixture.ReviewerMemberId),
                fixture.ReviewerMemberId, At(approvedOn).AddMinutes(1)));
            Assert.Null(policy.Approve(fixture.ProgramId, 1, approvalId, reviewId,
                approvedOn, true, "Approve the policy.", null,
                Actor(fixture.ApproverMemberId), fixture.ApproverMemberId, approvedAt));
            return Result.Success;
        });
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyApproved(fixture.TenantId, fixture.ProgramId, policyId, 1, 1, true,
                approvalId, reviewId, "content-hash", approvedOn, null, null,
                "Approve the policy.", Actor(fixture.ApproverMemberId),
                fixture.ApproverMemberId, approvedAt, null));

        var retirementReviewId = Uuid.CreateVersion4();
        var proposedAt = At(fixture.Today);
        var effectiveUntil = fixture.Today.AddMonths(12);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Policy(fixture.TenantId,
            policyId), policy =>
        {
            Assert.Null(policy.ProposeRetirement(fixture.ProgramId, 1, effectiveUntil,
                "The policy is replaced.", Actor(fixture.LeadMemberId), fixture.LeadMemberId,
                proposedAt));
            return Result.Success;
        });

        // Act
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyRetirementProposed(fixture.TenantId, fixture.ProgramId, policyId, 2, 1,
                effectiveUntil, "The policy is replaced.", Actor(fixture.LeadMemberId),
                fixture.LeadMemberId, proposedAt));
        var retirementReview = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, fixture.Today, fixture.Today.AddDays(30), null)).Value);
        var retirementReviewedAt = At(fixture.Today).AddMinutes(1);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Policy(fixture.TenantId,
            policyId), policy =>
        {
            Assert.Null(policy.Review(fixture.ProgramId, 2, retirementReviewId, "accept",
                "Retirement is appropriate.", Actor(fixture.ReviewerMemberId),
                fixture.ReviewerMemberId, retirementReviewedAt));
            return Result.Success;
        });
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyReviewed(fixture.TenantId, fixture.ProgramId, policyId, 2,
                retirementReviewId, "accept", "Retirement is appropriate.",
                Actor(fixture.ReviewerMemberId), fixture.ReviewerMemberId,
                retirementReviewedAt, null));
        var retirementApproval = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, fixture.Today, fixture.Today.AddDays(30), null)).Value);
        var retirementId = Uuid.CreateVersion4();
        var retiredAt = At(fixture.Today).AddMinutes(2);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Policy(fixture.TenantId,
            policyId), policy =>
        {
            Assert.Null(policy.ApproveRetirement(fixture.ProgramId, 2, retirementId,
                retirementReviewId, "The replacement is active.",
                Actor(fixture.ApproverMemberId), fixture.ApproverMemberId, retiredAt));
            return Result.Success;
        });
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyRetired(fixture.TenantId, fixture.ProgramId, policyId, 2, 1,
                retirementId, retirementReviewId, effectiveUntil,
                "The replacement is active.", Actor(fixture.ApproverMemberId),
                fixture.ApproverMemberId, retiredAt, null));
        var retired = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            fixture.Today, fixture.Today.AddDays(30), null);

        // Assert
        Assert.Equal("policy_retirement_review", retirementReview.Kind);
        Assert.Equal("policy_retirement_approval", retirementApproval.Kind);
        Assert.NotEqual(retirementReview.WorkItemId, retirementApproval.WorkItemId);
        Assert.True(retired.IsSuccess);
        Assert.Empty(retired.Value);
        Assert.Equal(5, await directory.LoadRevisionAsync(fixture.TenantId));
    }

    [Fact]
    public async Task ShouldReopenReturnedDraftWithNewIdentityGivenRequestedChangesThenRevision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var policyId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, policyId, 12);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzPolicyDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>(), TimeProvider.System);
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyDraftCreated(fixture.TenantId, fixture.ProgramId, policyId,
                Uuid.CreateVersion4(), "POL-WORK", Content(12), "content-hash",
                Actor(fixture.LeadMemberId), fixture.LeadMemberId, At(fixture.Today)));
        var original = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, fixture.Today, fixture.Today.AddDays(30), null)).Value);

        var reviewId = Uuid.CreateVersion4();
        var reviewedAt = At(fixture.Today).AddMinutes(1);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Policy(fixture.TenantId,
            policyId), policy =>
        {
            Assert.Null(policy.Review(fixture.ProgramId, 1, reviewId, "request_changes",
                "Clarify the owner responsibilities.", Actor(fixture.ReviewerMemberId),
                fixture.ReviewerMemberId, reviewedAt));
            return Result.Success;
        });
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyReviewed(fixture.TenantId, fixture.ProgramId, policyId, 1, reviewId,
                "request_changes", "Clarify the owner responsibilities.",
                Actor(fixture.ReviewerMemberId), fixture.ReviewerMemberId, reviewedAt, null));
        var returned = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            fixture.Today, fixture.Today.AddDays(30), null);

        var revisedAt = At(fixture.Today).AddMinutes(2);
        var revisedContent = Content(12, "Revised Access Control Policy");
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Policy(fixture.TenantId,
            policyId), policy =>
        {
            Assert.Null(policy.Revise(fixture.ProgramId, 1, revisedContent,
                Actor(fixture.LeadMemberId), fixture.LeadMemberId, revisedAt));
            return Result.Success;
        });

        // Act
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyDraftRevised(fixture.TenantId, fixture.ProgramId, policyId, 2,
                revisedContent, "revised-content-hash", null,
                Actor(fixture.LeadMemberId), fixture.LeadMemberId, revisedAt));
        var revised = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            fixture.Today, fixture.Today.AddDays(30), null);

        // Assert
        Assert.True(returned.IsSuccess);
        Assert.Empty(returned.Value);
        Assert.True(revised.IsSuccess);
        var reopened = Assert.Single(revised.Value);
        Assert.Equal("policy_draft_review", reopened.Kind);
        Assert.Contains("revision 2", reopened.Reason, StringComparison.Ordinal);
        Assert.NotEqual(original.WorkItemId, reopened.WorkItemId);
    }

    [Fact]
    public async Task ShouldKeepDuePeriodicReviewBesideSuccessorDraftGivenCurrentApprovedVersion()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var policyId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, policyId, 1);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzPolicyDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>(), TimeProvider.System);
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyDraftCreated(fixture.TenantId, fixture.ProgramId, policyId,
                Uuid.CreateVersion4(), "POL-WORK", Content(1), "content-hash",
                Actor(fixture.LeadMemberId), fixture.LeadMemberId, At(fixture.Today)));

        var reviewId = Uuid.CreateVersion4();
        var approvalId = Uuid.CreateVersion4();
        var approvedOn = fixture.Today.AddMonths(-2);
        var approvedAt = At(approvedOn).AddMinutes(2);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Policy(fixture.TenantId,
            policyId), policy =>
        {
            Assert.Null(policy.Review(fixture.ProgramId, 1, reviewId, "accept",
                "The policy draft is ready.", Actor(fixture.ReviewerMemberId),
                fixture.ReviewerMemberId, At(approvedOn).AddMinutes(1)));
            Assert.Null(policy.Approve(fixture.ProgramId, 1, approvalId, reviewId,
                approvedOn, true, "Approve the policy.", null,
                Actor(fixture.ApproverMemberId), fixture.ApproverMemberId, approvedAt));
            return Result.Success;
        });
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyApproved(fixture.TenantId, fixture.ProgramId, policyId, 1, 1, true,
                approvalId, reviewId, "content-hash", approvedOn, null, null,
                "Approve the policy.", Actor(fixture.ApproverMemberId),
                fixture.ApproverMemberId, approvedAt, null));
        var revisedAt = At(fixture.Today);
        var revisedContent = Content(1, "Successor Access Control Policy");
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Policy(fixture.TenantId,
            policyId), policy =>
        {
            Assert.Null(policy.ProposeSuccessor(fixture.ProgramId, 1, revisedContent,
                Actor(fixture.LeadMemberId), fixture.LeadMemberId, revisedAt));
            return Result.Success;
        });

        // Act
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyDraftRevised(fixture.TenantId, fixture.ProgramId, policyId, 2,
                revisedContent, "successor-content-hash", 1,
                Actor(fixture.LeadMemberId), fixture.LeadMemberId, revisedAt));
        var work = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            fixture.Today, fixture.Today.AddDays(30), null);

        // Assert
        Assert.True(work.IsSuccess);
        Assert.Collection(work.Value.OrderBy(static item => item.Kind, StringComparer.Ordinal),
            item => Assert.Equal("policy_draft_review", item.Kind),
            item => Assert.Equal("policy_periodic_review", item.Kind));
        Assert.Equal(2, work.Value.Select(static item => item.WorkItemId).Distinct().Count());
    }

    [Fact]
    public async Task ShouldRemovePendingWorkAfterDraftDiscardGivenCurrentProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var policyId = Uuid.CreateVersion4();
        await SeedDraftAsync(fixture, policyId, 12);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzPolicyDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>(), TimeProvider.System);
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyDraftCreated(fixture.TenantId, fixture.ProgramId, policyId,
                Uuid.CreateVersion4(), "POL-WORK", Content(12), "content-hash",
                Actor(fixture.LeadMemberId), fixture.LeadMemberId, At(fixture.Today)));
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Policy(fixture.TenantId,
            policyId), policy =>
        {
            Assert.Null(policy.Discard(fixture.ProgramId, 1, "No longer required.",
                Actor(fixture.LeadMemberId), At(fixture.Today).AddMinutes(1)));
            return Result.Success;
        });

        // Act
        await ProjectAsync(directory, events, fixture.TenantId,
            new PolicyDraftDiscarded(fixture.TenantId, fixture.ProgramId, policyId, 1,
                "No longer required.", Actor(fixture.LeadMemberId), At(fixture.Today).AddMinutes(1)));
        var work = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            fixture.Today, fixture.Today.AddDays(30), null);

        // Assert
        Assert.True(work.IsSuccess);
        Assert.Empty(work.Value);
        Assert.Equal(2, await directory.LoadRevisionAsync(fixture.TenantId));
    }

    [Fact]
    public async Task ShouldRejectLaggingPolicyWorkProjectionGivenPendingPolicyEvent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await SeedDraftAsync(fixture, Uuid.CreateVersion4(), 12);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzPolicyDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<IAggregateReader>(), TimeProvider.System);
        var consistency = new WorkQueueReadConsistency(fixture.Boundaries, events, [directory]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    static async Task SeedDraftAsync(OperationsFixture fixture, Uuid policyId, int cadenceMonths)
    {
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Policy(fixture.TenantId,
            policyId), policy =>
        {
            Assert.True(policy.Create(fixture.ProgramId, Uuid.CreateVersion4(), "POL-WORK",
                Content(cadenceMonths), Actor(fixture.LeadMemberId), fixture.LeadMemberId,
                At(fixture.Today)).IsSuccess);
            return Result.Success;
        });
    }

    static async Task ProjectAsync(FitzPolicyDecisionWorkItemDirectory directory,
        IDomainEventReader events, Uuid tenantId, DomainEvent domainEvent)
    {
        var identity = new CheckpointIdentity(FitzPolicyDecisionWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), "policies"));
        var current = await directory.LoadCheckpointAsync(tenantId);
        var source = await ReadPolicyCheckpointAsync(events, tenantId);
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            current));
        await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(source);
    }

    static async Task<ProjectionCheckpoint> ReadPolicyCheckpointAsync(IDomainEventReader events,
        Uuid tenantId)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           tenantId.ToString(), "policies"), cursor, CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    static PolicyContent Content(int cadenceMonths, string title = "Access Control Policy") => new(title,
        "Govern access", PolicyAudience.CoreSecurity, null, cadenceMonths, "Policy body", null,
        "Security owner", []);

    static ActorReference Actor(Uuid memberId) => ActorReference.ForMember(memberId,
        memberId.ToString());

    static DateTimeOffset At(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
