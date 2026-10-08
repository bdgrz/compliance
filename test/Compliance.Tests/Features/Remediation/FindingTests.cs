using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.Readiness;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class FindingTests
{
    [Fact]
    public async Task ShouldCloseAndReopenWithHistoryGivenVerifiedRemediation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await RaiseWithCompletedActionAsync(fixture);

        // Act
        var closed = (await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId, Close(fixture, finding))).Value;
        var reopened = await fixture.AsAsync(fixture.LeadUserId, new ReopenFinding(
            fixture.TenantId, fixture.ProgramId, finding.FindingId, closed.Revision,
            "A leaver regained access."));

        // Assert
        Assert.Equal("closed", closed.Status);
        Assert.Equal("closed", closed.ReadinessStatus);
        Assert.Equal(fixture.ApproverMemberId, closed.Closure!.CloserMemberId);
        Assert.All(closed.Closure.ResolutionEvidence, item => Assert.Equal("unresolved", item.Resolution));
        Assert.Equal("remediated", reopened.Status);
        Assert.Null(reopened.Closure);
        Assert.Equal(["raised", "corrective_action_added", "corrective_action_completed", "closed",
            "reopened"], reopened.History.Select(entry => entry.Change));
    }

    [Fact]
    public async Task ShouldRejectClosureGivenAcceptanceInsteadOfRemediation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var registration = await fixture.AsAsync(fixture.LeadUserId, fixture.Raise(fixture.OwnerMemberId));
        var waiverId = await fixture.ApprovedWaiverAsync(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Finding, registration.FindingId,
            registration.FindingId, 1, SeparationOfDutiesActions.Approve), fixture.LeadMemberId);
        var accepted = await fixture.AsAsync(fixture.LeadUserId, new LinkFindingAcceptance(
            fixture.TenantId, fixture.ProgramId, registration.FindingId, 1, "waiver", waiverId));

        // Act
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId, Close(fixture, accepted), RequestErrorKind.Conflict);

        // Assert
        Assert.Equal("accepted", accepted.ReadinessStatus);
        Assert.Equal("active", Assert.Single(accepted.Acceptances).State);
    }

    [Fact]
    public void ShouldReturnToActionableStateGivenExpiredAcceptance()
    {
        // Arrange
        var ledger = new RemediationLedger(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var findingId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        Assert.Null(ledger.Raise(findingId, new FindingSource("manual", null, null, "Gap."),
            "Gap", "Gap found.", "medium", "All", Uuid.CreateVersion4(),
            DateOnly.FromDateTime(now.UtcDateTime).AddDays(10), [], actor, now));
        Assert.Null(ledger.LinkAcceptance(findingId, 1, "waiver", Uuid.CreateVersion4(), null,
            now.AddDays(30), actor, now));

        // Act
        var during = ledger.Read(findingId, now.AddDays(1))!;
        var after = ledger.Read(findingId, now.AddDays(31))!;

        // Assert
        Assert.Equal("accepted", during.ReadinessStatus);
        Assert.Equal("overdue", after.ReadinessStatus);
        Assert.Equal("expired", after.Acceptances[0].State);
        Assert.NotNull(ledger.LinkAcceptance(findingId, 2, "waiver", Uuid.CreateVersion4(), null,
            now.AddDays(-1), actor, now));
    }

    [Fact]
    public async Task ShouldRejectClosureGivenOwnerVerifiesOwnRemediation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await RaiseWithCompletedActionAsync(fixture, fixture.LeadMemberId);

        // Act
        var owner = await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.LeadUserId, Close(fixture, finding), RequestErrorKind.Forbidden);
        var performer = await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.OwnerUserId, Close(fixture, finding), RequestErrorKind.Forbidden);

        // Assert
        Assert.False(owner.IsSuccess);
        Assert.False(performer.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectClosureGivenMissingResolutionEvidence()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await RaiseWithCompletedActionAsync(fixture);

        // Act
        var result = await PersonalReadinessClosureTransportTests.CloseHttpAsync(fixture.Provider, fixture.ApproverUserId, Close(fixture, finding) with { ResolutionEvidence = [] }, RequestErrorKind.Validation);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ShouldPreserveSourceTextAndAttributeChangeGivenTriageRevision()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var registration = await fixture.AsAsync(fixture.LeadUserId, fixture.Raise(fixture.OwnerMemberId));

        // Act
        var revised = await fixture.AsAsync(fixture.LeadUserId, new ReviseFinding(
            fixture.TenantId, fixture.ProgramId, registration.FindingId, 1, "critical",
            fixture.BackupMemberId, fixture.Today.AddDays(3), "VPN and SSO", "Manual offboarding",
            "Scope widened after triage."));

        // Assert
        Assert.Equal("Terminated users kept VPN access.", revised.Source.SourceText);
        Assert.Equal("critical", revised.Severity);
        Assert.Equal(fixture.BackupMemberId, revised.OwnerMemberId);
        var change = revised.History[^1];
        Assert.Equal("revised", change.Change);
        Assert.Equal(fixture.LeadMemberId.ToString(), change.Actor.Id);
        Assert.Contains("Scope widened after triage.", change.Summary, StringComparison.Ordinal);
        await fixture.Scenario(fixture.OwnerUserId).When(new ReviseFinding(fixture.TenantId,
                fixture.ProgramId, registration.FindingId, 2, "low", fixture.OwnerMemberId,
                fixture.Today, "VPN", null, "Downgrade."))
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldRejectGovernedSourceGivenUnknownRecord()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        await fixture.Scenario(fixture.LeadUserId)
            // Act
            .When(fixture.Raise(fixture.OwnerMemberId) with
            {
                Source = new FindingSource("readiness_gap", Uuid.CreateVersion4(),
                    Uuid.CreateVersion4().ToString(), "Unmapped criterion."),
            })
            // Assert
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.LeadUserId)
            .When(fixture.Raise(fixture.OwnerMemberId) with
            {
                Source = new FindingSource("control_occurrence", Uuid.CreateVersion4(), null,
                    "Failed."),
            })
            .ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldRouteCorrectiveActionToOwnerQueueGivenAssignment()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var registration = await fixture.AsAsync(fixture.LeadUserId, fixture.Raise(fixture.LeadMemberId));

        // Act
        var finding = await fixture.AsAsync(fixture.LeadUserId, new AddCorrectiveAction(
            fixture.TenantId, fixture.ProgramId, registration.FindingId, 1,
            "Remove stale VPN accounts.", fixture.OwnerMemberId, fixture.Today.AddDays(-1)));

        // Assert
        var work = await fixture.AsAsync(fixture.OwnerUserId, new ListMyControlWork(
            fixture.TenantId, fixture.ProgramId));
        var item = Assert.Single(work.Items);
        Assert.Equal("corrective_action", item.Kind);
        Assert.Equal(finding.FindingId, item.FindingId);
        Assert.Equal("high", item.Materiality);
        Assert.True(item.Overdue);
        await fixture.Scenario(fixture.OutsiderUserId).When(new CompleteCorrectiveAction(
                fixture.TenantId, fixture.ProgramId, finding.FindingId, 2,
                finding.CorrectiveActions[0].ActionId, "Done.",
                [new EvidenceReference(null, "record", "VPN-EXPORT")]))
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldListByReadinessStatusGivenMixedFindings()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.AsAsync(fixture.LeadUserId, fixture.Raise(fixture.OwnerMemberId,
            fixture.Today.AddDays(-3)));
        await RaiseWithCompletedActionAsync(fixture);

        // Act
        var overdue = await fixture.AsAsync(fixture.OutsiderUserId, new ListFindings(
            fixture.TenantId, fixture.ProgramId, "overdue"));
        var remediated = await fixture.AsAsync(fixture.OutsiderUserId, new ListFindings(
            fixture.TenantId, fixture.ProgramId, "remediated"));

        // Assert
        Assert.Single(overdue.Items);
        Assert.Single(remediated.Items);
        await fixture.Scenario(fixture.OutsiderUserId).When(new ListFindings(fixture.TenantId,
                fixture.ProgramId, "maybe"))
            .ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldRejectStaleRevisionGivenConcurrentFindingChanges()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var registration = await fixture.AsAsync(fixture.LeadUserId, fixture.Raise(fixture.OwnerMemberId));
        await fixture.AsAsync(fixture.LeadUserId, new AddCorrectiveAction(fixture.TenantId,
            fixture.ProgramId, registration.FindingId, 1, "Fix.", fixture.OwnerMemberId,
            fixture.Today.AddDays(5)));

        await fixture.Scenario(fixture.ApproverUserId)
            // Act
            .When(new AddCorrectiveAction(fixture.TenantId, fixture.ProgramId,
                registration.FindingId, 1, "Fix again.", fixture.OwnerMemberId,
                fixture.Today.AddDays(5)))
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
    }

    internal static async Task<FindingView> RaiseWithCompletedActionAsync(OperationsFixture fixture,
        Uuid? findingOwner = null)
    {
        var registration = await fixture.AsAsync(fixture.LeadUserId,
            fixture.Raise(findingOwner ?? fixture.OwnerMemberId));
        var finding = await fixture.AsAsync(fixture.LeadUserId, new AddCorrectiveAction(
            fixture.TenantId, fixture.ProgramId, registration.FindingId, 1,
            "Remove stale VPN accounts.", fixture.OwnerMemberId, fixture.Today.AddDays(7)));
        return await fixture.AsAsync(fixture.OwnerUserId, new CompleteCorrectiveAction(
            fixture.TenantId, fixture.ProgramId, finding.FindingId, finding.Revision,
            finding.CorrectiveActions[0].ActionId, "Removed both accounts.",
            [new EvidenceReference(null, "record", "VPN-EXPORT-2026-09")]));
    }

    internal static CloseFinding Close(OperationsFixture fixture, FindingView finding) => new(
        fixture.TenantId, fixture.ProgramId, finding.FindingId, finding.Revision,
        "Re-ran the VPN export; no leavers remain.",
        [new EvidenceReference(null, "external", "https://vpn.example/export")],
        "Remediation verified.");
}
