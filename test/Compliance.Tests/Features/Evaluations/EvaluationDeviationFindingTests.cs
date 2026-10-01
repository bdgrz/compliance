using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Evaluations;

public sealed class EvaluationDeviationFindingTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid EvaluationId = Uuid.CreateVersion4();

    static EvaluationDeviationView Deviation(string classification) => new(
        Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, classification, "Leavers kept access.",
        classification == "material" ? "pending_routing" : "dispositioned", null, null, null, null,
        ActorReference.ForMember(Uuid.CreateVersion4(), "Owner"), DateTimeOffset.UtcNow);

    [Fact]
    public async Task ShouldRouteOnlyMaterialDeviationsGivenSubmission()
    {
        // Arrange
        var material = Deviation("material");
        var submission = new EvaluationSubmissionView(1, [], "ineffective", [],
            [material, Deviation("minor")], ActorReference.ForMember(Uuid.CreateVersion4(), "Owner"),
            DateTimeOffset.UtcNow);
        var scenario = new ReactorScenario().Given(new ControlEvaluationSubmitted(TenantId,
            ProgramId, ControlId, EvaluationId, 5, submission));

        // Act
        await scenario.RunAsync(new ControlEvaluationDeviationFindingReactor(
            new InMemoryProjectionCheckpointStore(), scenario.Requests));

        // Assert
        var request = Assert.IsType<RaiseEvaluationDeviationFinding>(
            Assert.Single(scenario.SentRequests));
        Assert.Equal(material.DeviationId, request.DeviationId);
        Assert.Equal(Uuid.CreateVersion5(material.DeviationId, "finding"), request.FindingId);
    }

    [Fact]
    public async Task ShouldRaiseOwnedFindingWithCorrectiveActionIdempotentlyGivenSystemReaction()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await fixture.AsAsync(fixture.OwnerUserId, new StartControlEvaluation(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId,
            [new("implementation", "reperformance", [new("artifact", "exports/q3.csv", "sha256:1")],
                "Every leaver was removed.")]));
        evaluation = await fixture.AsAsync(fixture.OwnerUserId, new RecordControlEvaluationStep(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId,
            evaluation.Steps[0].StepId, evaluation.Revision, "not_met", "Two leavers active.",
            evaluation.Steps[0].InspectedItems, "material", "Two leavers kept access."));
        evaluation = await fixture.AsAsync(fixture.OwnerUserId, new SubmitControlEvaluation(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId,
            evaluation.Revision,
            [new("design", "not_tested", "Out of scope."),
                new("implementation", "ineffective", "Leavers kept access."),
                new("evidence_sufficiency", "not_tested", "Out of scope.")]));
        var deviation = Assert.Single(evaluation.Deviations);
        var reaction = new RaiseEvaluationDeviationFinding(fixture.TenantId, fixture.ProgramId,
            Uuid.CreateVersion5(deviation.DeviationId, "finding"), fixture.ControlId,
            evaluation.EvaluationId, deviation.DeviationId);
        await RequestScenario.For(fixture.Provider).GivenActor(RequestActor.System)
            .When(reaction).ExpectSuccess();

        // Act
        await RequestScenario.For(fixture.Provider).GivenActor(RequestActor.System)
            .When(reaction).ExpectSuccess();

        // Assert
        var finding = await fixture.GetFindingAsync(reaction.FindingId);
        Assert.Equal("evaluation_deviation", finding.Source.Kind);
        Assert.Equal(deviation.DeviationId, finding.Source.RecordId);
        Assert.Equal("Two leavers kept access.", finding.Source.SourceText);
        Assert.Equal(fixture.OwnerMemberId, finding.OwnerMemberId);
        Assert.Single(finding.CorrectiveActions);
        Assert.Contains(new FindingLink("control_evaluation", evaluation.EvaluationId.ToString()),
            finding.Links);
        var findings = await fixture.AsAsync(fixture.OutsiderUserId, new ListFindings(
            fixture.TenantId, fixture.ProgramId));
        Assert.Single(findings.Items);
        var read = await fixture.AsAsync(fixture.OutsiderUserId, new GetControlEvaluation(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId));
        Assert.Equal("routed_to_finding", Assert.Single(read.Deviations).Status);
        await fixture.Scenario(fixture.LeadUserId).When(reaction)
            .ExpectFailure(RequestErrorKind.Forbidden);
    }
}
