using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class OccurrenceFindingTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid OccurrenceId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldRaiseDeterministicFindingGivenFailedAttestation()
    {
        // Arrange
        var attestation = Attestation("failed", "Export tool was down.");
        var scenario = new ReactorScenario().Given(new ControlOccurrenceAttested(TenantId,
            ProgramId, ControlId, OccurrenceId, 2, attestation));

        // Act
        await scenario.RunAsync(new ControlOccurrenceFindingReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        var request = Assert.IsType<RaiseOccurrenceFinding>(Assert.Single(scenario.SentRequests));
        Assert.Equal(Uuid.CreateVersion5(attestation.AttestationId, "finding"), request.FindingId);
        Assert.Equal("control_occurrence", request.SourceKind);
        Assert.Equal("failed: Export tool was down.", request.SourceText);
        Assert.Equal(attestation.AttestationId.ToString(), request.SourceVersion);
    }

    [Fact]
    public async Task ShouldIgnoreCompleteAndNotApplicableResultsGivenAttestation()
    {
        // Arrange
        var scenario = new ReactorScenario()
            .Given(new ControlOccurrenceAttested(TenantId, ProgramId, ControlId, OccurrenceId, 2,
                Attestation("complete", null)))
            .Given(new ControlOccurrenceAttested(TenantId, ProgramId, ControlId, OccurrenceId, 3,
                Attestation("not_applicable", "No changes shipped.")));

        // Act
        await scenario.RunAsync(new ControlOccurrenceFindingReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        Assert.Empty(scenario.SentRequests);
    }

    [Fact]
    public async Task ShouldRaiseOneFindingPerRequestedActionGivenActionRequestedReview()
    {
        // Arrange
        var decisionId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario().Given(new ControlOccurrenceReviewed(TenantId,
            ProgramId, ControlId, OccurrenceId, 3, new ControlOccurrenceReviewView(decisionId,
                Uuid.CreateVersion4(), 1, "action_requested", "Two follow-ups.",
                ["Automate removal.", "Train managers."], Uuid.CreateVersion4(),
                ActorReference.ForMember(Uuid.CreateVersion4(), "Reviewer"),
                DateTimeOffset.UtcNow)));

        // Act
        await scenario.RunAsync(new ControlOccurrenceFindingReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        var requests = scenario.SentRequests.Cast<RaiseOccurrenceFinding>().ToArray();
        Assert.Equal(["Automate removal.", "Train managers."], requests.Select(r => r.SourceText));
        Assert.All(requests, request => Assert.Equal("occurrence_review", request.SourceKind));
        Assert.Equal(2, requests.Select(request => request.FindingId).Distinct().Count());
    }

    [Fact]
    public async Task ShouldConvertExceptionIdempotentlyGivenSystemReaction()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        var failed = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed, "failed",
            [], "Export tool was down."));
        var attestationId = failed.Attestations[0].AttestationId;
        var reaction = new RaiseOccurrenceFinding(fixture.TenantId, fixture.ProgramId,
            Uuid.CreateVersion5(attestationId, "finding"), fixture.ControlId,
            failed.OccurrenceId, attestationId.ToString(), "control_occurrence",
            "failed: Export tool was down.");
        await RequestScenario.For(fixture.Provider).GivenActor(RequestActor.System)
            .When(reaction).ExpectSuccess();

        // Act
        await RequestScenario.For(fixture.Provider).GivenActor(RequestActor.System)
            .When(reaction).ExpectSuccess();

        // Assert
        var finding = await fixture.GetFindingAsync(reaction.FindingId);
        Assert.Equal("control_occurrence", finding.Source.Kind);
        Assert.Equal(failed.OccurrenceId, finding.Source.RecordId);
        Assert.Equal("failed: Export tool was down.", finding.Source.SourceText);
        Assert.Equal(fixture.OwnerMemberId, finding.OwnerMemberId);
        Assert.Equal("system_process", finding.RaisedBy.Kind);
        var findings = await fixture.AsAsync(fixture.OutsiderUserId, new ListFindings(
            fixture.TenantId, fixture.ProgramId));
        Assert.Single(findings.Items);
        await fixture.Scenario(fixture.LeadUserId).When(reaction)
            .ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldCreateCorrectiveActionGivenManagementRequestedAction()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var missed = (await fixture.OccurrencesAsync("missed"))[0];
        var submitted = await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(missed));
        var reviewed = await fixture.AsAsync(fixture.ReviewerUserId,
            fixture.Review(submitted, "action_requested", ["Automate leaver removal."]));
        var decisionId = reviewed.Reviews[0].DecisionId;
        var findingId = Uuid.CreateVersion5(decisionId, "requested-action-0");

        // Act
        await RequestScenario.For(fixture.Provider).GivenActor(RequestActor.System)
            .When(new RaiseOccurrenceFinding(fixture.TenantId, fixture.ProgramId, findingId,
                fixture.ControlId, reviewed.OccurrenceId, decisionId.ToString(),
                "occurrence_review", "Automate leaver removal."))
            .ExpectSuccess();

        // Assert
        var finding = await fixture.GetFindingAsync(findingId);
        Assert.Equal(decisionId.ToString(), finding.Source.Version);
        var action = Assert.Single(finding.CorrectiveActions);
        Assert.Equal("Automate leaver removal.", action.Description);
        Assert.Equal(fixture.OwnerMemberId, action.OwnerMemberId);
        var work = await fixture.AsAsync(fixture.OwnerUserId, new ListMyControlWork(
            fixture.TenantId, fixture.ProgramId));
        Assert.Contains(work.Items, item => item.Kind == "corrective_action" &&
            item.FindingId == findingId);
    }

    static ControlAttestationView Attestation(string result, string? rationale) => new(
        Uuid.CreateVersion4(), 1, result, DateTimeOffset.UtcNow, null, null, null, rationale, [],
        new OperatingHolder("member", Uuid.CreateVersion4()), Uuid.CreateVersion4(),
        ActorReference.ForMember(Uuid.CreateVersion4(), "Owner"), DateTimeOffset.UtcNow,
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), []);
}
