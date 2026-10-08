using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class ControlMappingWorkTests
{
    [Fact]
    public async Task ShouldLimitPendingMappingDecisionsToEligibleProgramMembersGivenProgram()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var decisions = await SeedPendingDecisionsAsync(fixture);

        // Act
        var managerQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var repeatedQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var proposerQueue = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var contributorQueue = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var otherProgramQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, Uuid.CreateVersion4(), "unassigned"));
        var otherTenantQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(Uuid.CreateVersion4(), fixture.ProgramId, "unassigned"));

        // Assert
        Assert.Equal(2, managerQueue.Items.Count);
        Assert.Contains(managerQueue.Items,
            item => item.Kind == "control_criterion_mapping_review" &&
                    item.SourceId == decisions.MappingId);
        Assert.Contains(managerQueue.Items,
            item => item.Kind == "criterion_applicability_review" &&
                    item.SourceId == decisions.ApplicabilityId);
        Assert.Equal(managerQueue.Items.Select(item => item.WorkItemId),
            repeatedQueue.Items.Select(item => item.WorkItemId));
        Assert.Empty(proposerQueue.Items);
        Assert.Empty(contributorQueue.Items);
        Assert.Empty(otherProgramQueue.Items);
        Assert.Empty(otherTenantQueue.Items);
    }

    [Fact]
    public async Task ShouldShowPendingControlMappingGivenIndependentProgramManager()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await SeedPendingDecisionsAsync(fixture);

        // Act
        var queue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        Assert.Single(queue.Items, item => item.Kind == "control_criterion_mapping_review");
    }

    [Fact]
    public async Task ShouldLinkPendingControlMappingToReviewActionGivenIndependentProgramManager()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var decisions = await SeedPendingDecisionsAsync(fixture);

        // Act
        var queue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        var item = Assert.Single(queue.Items,
            candidate => candidate.Kind == "control_criterion_mapping_review");
        Assert.Equal(decisions.MappingId, item.SourceId);
        Assert.Equal(fixture.ControlId, item.ControlId);
        Assert.Equal("review", item.NextAction);
        Assert.Null(item.AssigneeMemberId);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramManagerHolder,
            fixture.ProgramId), item.Responsible);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"control-mappings/{decisions.MappingId}/reviews", item.ActionPath);
    }

    [Fact]
    public async Task ShouldShowPendingApplicabilityDecisionGivenIndependentProgramManager()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await SeedPendingDecisionsAsync(fixture);

        // Act
        var queue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        Assert.Single(queue.Items, item => item.Kind == "criterion_applicability_review");
    }

    [Fact]
    public async Task ShouldLinkPendingApplicabilityDecisionToReviewActionGivenIndependentProgramManager()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var decisions = await SeedPendingDecisionsAsync(fixture);

        // Act
        var queue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        var item = Assert.Single(queue.Items,
            candidate => candidate.Kind == "criterion_applicability_review");
        Assert.Equal(decisions.ApplicabilityId, item.SourceId);
        Assert.Null(item.ControlId);
        Assert.Equal("review", item.NextAction);
        Assert.Null(item.AssigneeMemberId);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramManagerHolder,
            fixture.ProgramId), item.Responsible);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"criterion-applicability/{decisions.ApplicabilityId}/reviews",
            item.ActionPath);
    }

    [Fact]
    public async Task ShouldRemoveMappingDecisionsGivenReviewCompleted()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var decisions = await SeedPendingDecisionsAsync(fixture);
        var before = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var now = DateTimeOffset.UtcNow;
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new ControlCriterionMappingLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.Review(decisions.MappingId, 1, Uuid.CreateVersion4(), "accept",
                    "The mapping applies.", fixture.ApproverMemberId, "Approver", now));
                return Result.Success;
            });
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new CriterionApplicabilityLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.Review(decisions.ApplicabilityId, 1, Uuid.CreateVersion4(),
                    "reject", "The criterion applies.", fixture.ApproverMemberId,
                    ActorReference.ForMember(fixture.ApproverMemberId, "Approver"), now));
                return Result.Success;
            });

        // Act
        var after = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        Assert.Contains(before.Items, item => item.Kind == "control_criterion_mapping_review");
        Assert.Contains(before.Items, item => item.Kind == "criterion_applicability_review");
        Assert.DoesNotContain(after.Items, item => item.Kind is
            "control_criterion_mapping_review" or "criterion_applicability_review");
    }

    [Fact]
    public async Task ShouldCreateNewWorkIdentityGivenMappingDecisionsResubmitted()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var decisions = await SeedPendingDecisionsAsync(fixture);
        var firstQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var firstMapping = Assert.Single(firstQueue.Items,
            item => item.Kind == "control_criterion_mapping_review");
        var firstApplicability = Assert.Single(firstQueue.Items,
            item => item.Kind == "criterion_applicability_review");
        var now = DateTimeOffset.UtcNow;
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new ControlCriterionMappingLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.Review(decisions.MappingId, 1, Uuid.CreateVersion4(), "reject",
                    "Revise the applicability rationale.", fixture.ApproverMemberId, "Approver",
                    now));
                Assert.Null(ledger.Propose(fixture.ControlId, fixture.ControlVersionId,
                    decisions.EditionId, "CC6.1", "criterion", 2, "Revised mapping rationale.",
                    "Applies to production systems.", fixture.LeadMemberId, "Lead",
                    now.AddMinutes(1), out _));
                return Result.Success;
            });
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new CriterionApplicabilityLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.Review(decisions.ApplicabilityId, 1, Uuid.CreateVersion4(),
                    "reject", "Revise the rationale.", fixture.ApproverMemberId,
                    ActorReference.ForMember(fixture.ApproverMemberId, "Approver"), now));
                Assert.Null(ledger.Propose(decisions.EditionId, "CC6.2", 2,
                    "Revised not-applicable rationale.", fixture.LeadMemberId,
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"), now.AddMinutes(1),
                    out _));
                return Result.Success;
            });

        // Act
        var resubmittedQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        var secondMapping = Assert.Single(resubmittedQueue.Items,
            item => item.Kind == "control_criterion_mapping_review");
        var secondApplicability = Assert.Single(resubmittedQueue.Items,
            item => item.Kind == "criterion_applicability_review");
        Assert.NotEqual(firstMapping.WorkItemId, secondMapping.WorkItemId);
        Assert.NotEqual(firstApplicability.WorkItemId, secondApplicability.WorkItemId);
    }

    static async Task<PendingDecisions> SeedPendingDecisionsAsync(OperationsFixture fixture)
    {
        var editionId = Uuid.CreateVersion4();
        var mappingId = ControlCriterionMappingLedger.MappingIdFor(fixture.ProgramId,
            fixture.ControlId, editionId, "CC6.1");
        var applicabilityId = CriterionApplicabilityLedger.DecisionIdFor(fixture.ProgramId,
            editionId, "CC6.2");
        var now = DateTimeOffset.UtcNow;
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new ControlCriterionMappingLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.Propose(fixture.ControlId, fixture.ControlVersionId, editionId,
                    "CC6.1", "criterion", 0, "The control addresses access review.",
                    "Applies to production systems.", fixture.LeadMemberId, "Lead", now,
                    out _));
                return Result.Success;
            });
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new CriterionApplicabilityLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.Propose(editionId, "CC6.2", 0,
                    "The requirement does not apply to this organization.", fixture.LeadMemberId,
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"), now, out _));
                return Result.Success;
            });
        return new PendingDecisions(editionId, mappingId, applicabilityId);
    }

    sealed record PendingDecisions(Uuid EditionId, Uuid MappingId, Uuid ApplicabilityId);
}
