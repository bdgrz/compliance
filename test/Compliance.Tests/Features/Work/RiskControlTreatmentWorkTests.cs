using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class RiskControlTreatmentWorkTests
{
    [Fact]
    public async Task ShouldShowEachPendingTreatmentExactlyOnceToIndependentProgramManagerGivenProposal()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var riskId = Uuid.CreateVersion4();
        var treatmentId = Uuid.CreateVersion4();
        await SeedPendingTreatmentAsync(fixture, riskId, treatmentId);

        // Act
        var managerQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var proposerQueue = await fixture.AsAsync(fixture.LeadUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var contributorQueue = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        var item = Assert.Single(managerQueue.Items);
        Assert.Equal("risk_control_treatment_review", item.Kind);
        Assert.Equal(treatmentId, item.SourceId);
        Assert.Empty(proposerQueue.Items);
        Assert.Empty(contributorQueue.Items);
    }

    [Fact]
    public async Task ShouldIsolatePendingTreatmentReviewToItsTenantAndProgramGivenProposal()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await SeedPendingTreatmentAsync(fixture, Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var ownQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var otherProgramQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, Uuid.CreateVersion4(), "unassigned"));
        var otherTenantQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(Uuid.CreateVersion4(), fixture.ProgramId, "unassigned"));

        // Assert
        Assert.Single(ownQueue.Items,
            item => item.Kind == "risk_control_treatment_review");
        Assert.Empty(otherProgramQueue.Items);
        Assert.Empty(otherTenantQueue.Items);
    }

    [Fact]
    public async Task ShouldLinkPendingTreatmentReviewToExactSourceActionGivenProgramManager()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var riskId = Uuid.CreateVersion4();
        var treatmentId = Uuid.CreateVersion4();
        await SeedPendingTreatmentAsync(fixture, riskId, treatmentId);

        // Act
        var queue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var repeatedQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        var item = Assert.Single(queue.Items,
            candidate => candidate.Kind == "risk_control_treatment_review");
        Assert.Equal(treatmentId, item.SourceId);
        Assert.Equal("review", item.NextAction);
        Assert.Null(item.AssigneeMemberId);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramReviewerHolder,
            fixture.ProgramId), item.Responsible);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"risks/{riskId}/control-treatments/{treatmentId}/reviews", item.ActionPath);
        Assert.Equal(item.WorkItemId, Assert.Single(repeatedQueue.Items).WorkItemId);
    }

    [Fact]
    public async Task ShouldRemovePendingTreatmentReviewGivenAcceptedOrRejectedDecision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var acceptedRiskId = Uuid.CreateVersion4();
        var acceptedTreatmentId = Uuid.CreateVersion4();
        var rejectedRiskId = Uuid.CreateVersion4();
        var rejectedTreatmentId = Uuid.CreateVersion4();
        await SeedPendingTreatmentAsync(fixture, acceptedRiskId, acceptedTreatmentId);
        await SeedPendingTreatmentAsync(fixture, rejectedRiskId, rejectedTreatmentId);
        var before = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var now = DateTimeOffset.UtcNow;
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskGovernanceLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.ReviewControlTreatment(acceptedRiskId, acceptedTreatmentId,
                    1, Uuid.CreateVersion4(), "accept", "The control treats this risk.",
                    fixture.ApproverMemberId,
                    ActorReference.ForMember(fixture.ApproverMemberId, "Approver"), now));
                Assert.Null(ledger.ReviewControlTreatment(rejectedRiskId, rejectedTreatmentId,
                    1, Uuid.CreateVersion4(), "reject", "The control does not treat this risk.",
                    fixture.ApproverMemberId,
                    ActorReference.ForMember(fixture.ApproverMemberId, "Approver"), now));
                return Result.Success;
            });

        // Act
        var after = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        Assert.Equal(2, before.Items.Count(item => item.Kind == "risk_control_treatment_review"));
        Assert.DoesNotContain(after.Items,
            item => item.Kind == "risk_control_treatment_review");
    }

    [Fact]
    public async Task ShouldKeepTreatmentWorkIdentityStableAndDistinctGivenSameTreatmentIdInDifferentRisks()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var firstRiskId = Uuid.CreateVersion4();
        var secondRiskId = Uuid.CreateVersion4();
        var treatmentId = Uuid.CreateVersion4();
        await SeedPendingTreatmentAsync(fixture, firstRiskId, treatmentId);
        await SeedPendingTreatmentAsync(fixture, secondRiskId, treatmentId);

        // Act
        var queue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var repeatedQueue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));

        // Assert
        var items = queue.Items.Where(item => item.Kind == "risk_control_treatment_review")
            .ToArray();
        Assert.Equal(2, items.Length);
        Assert.Equal(2, items.Select(item => item.WorkItemId).Distinct().Count());
        var repeatedItems = repeatedQueue.Items
            .Where(item => item.Kind == "risk_control_treatment_review")
            .OrderBy(item => item.ActionPath, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(items.OrderBy(item => item.ActionPath, StringComparer.Ordinal)
            .Select(item => item.WorkItemId), repeatedItems.Select(item => item.WorkItemId));
        Assert.Contains(items, item => item.ActionPath.Contains(firstRiskId.ToString(),
            StringComparison.Ordinal));
        Assert.Contains(items, item => item.ActionPath.Contains(secondRiskId.ToString(),
            StringComparison.Ordinal));
    }

    static async Task SeedPendingTreatmentAsync(OperationsFixture fixture, Uuid riskId,
        Uuid treatmentId)
    {
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskGovernanceLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.ProposeControlTreatment(riskId, 0, treatmentId, "mitigate",
                    fixture.ControlId, fixture.ControlVersionId,
                    "This approved control treats the risk.", fixture.LeadMemberId,
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"),
                    DateTimeOffset.UtcNow));
                return Result.Success;
            });
    }
}
