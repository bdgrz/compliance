using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkQueueTests
{
    [Fact]
    public async Task ShouldListSourceLinkedItemsInDeterministicOrderGivenMixedSources()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var finding = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(3));

        // Act
        var queue = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Equal("mine", queue.Scope);
        Assert.Contains(queue.Items, item => item.Kind == "control_occurrence" && item.Overdue);
        var action = Assert.Single(queue.Items, item => item.Kind == "corrective_action");
        Assert.Equal(finding.CorrectiveActions[0].ActionId, action.SourceId);
        Assert.Equal(finding.FindingId, action.FindingId);
        Assert.Equal("complete", action.NextAction);
        Assert.EndsWith($"/corrective-actions/{action.SourceId}/completions", action.ActionPath,
            StringComparison.Ordinal);
        Assert.Equal("high", action.Materiality);
        Assert.Equal(fixture.OwnerMemberId, action.AssigneeMemberId);
        Assert.Equal(queue.Items.OrderBy(item => item.DueOn ?? DateOnly.MaxValue)
            .Select(item => item.WorkItemId), queue.Items.Select(item => item.WorkItemId));
        Assert.Equal(queue.Items.Count, queue.Counts.Total);
        Assert.Equal(queue.Items.Count(item => item.Overdue), queue.Counts.Overdue);
    }

    [Fact]
    public async Task ShouldRemoveItemGivenSourceWorkCompleted()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(3));
        var item = Assert.Single((await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId))).Items);

        // Act
        await fixture.AsAsync(fixture.OwnerUserId, new CompleteCorrectiveAction(fixture.TenantId,
            fixture.ProgramId, finding.FindingId, finding.Revision,
            finding.CorrectiveActions[0].ActionId, "Removed.",
            [new EvidenceReference(null, "record", "VPN-EXPORT")]));

        // Assert
        var after = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        Assert.Empty(after.Items);
        Assert.Equal(0, after.Counts.Total);
        await fixture.Scenario(fixture.OwnerUserId)
            .When(new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);
    }

    [Fact]
    public async Task ShouldOmitRestrictedWorkGivenUnrelatedMemberOrTenant()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);
        var item = Assert.Single((await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId))).Items);

        // Act
        var outsider = await fixture.AsAsync(fixture.OutsiderUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "all"));
        var otherTenant = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(Uuid.CreateVersion4(), fixture.ProgramId, "all"));

        // Assert
        Assert.Empty(outsider.Items);
        Assert.Equal(0, outsider.Counts.Total);
        Assert.Empty(otherTenant.Items);
        await fixture.Scenario(fixture.OutsiderUserId)
            .When(new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);
        await fixture.Scenario(fixture.OwnerUserId)
            .When(new GetWorkItem(Uuid.CreateVersion4(), fixture.ProgramId, item.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);
    }

    [Fact]
    public async Task ShouldRejectScopeGivenUnknownScope()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        await fixture.Scenario(fixture.OwnerUserId)
            // Act
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "everything"))
            // Assert
            .ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldListReviewForReviewerOnlyGivenSubmittedAttestation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];

        // Act
        await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));

        // Assert
        var reviewer = await fixture.AsAsync(fixture.ReviewerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        var review = Assert.Single(reviewer.Items);
        Assert.Equal("occurrence_review", review.Kind);
        Assert.Equal(missed.OccurrenceId, review.SourceId);
        var owner = await fixture.AsAsync(fixture.OwnerUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "all"));
        Assert.DoesNotContain(owner.Items, item => item.WorkItemId == review.WorkItemId);
    }
}
