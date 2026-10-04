using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class IndependenceCompartmentsTests
{
    static readonly Uuid ClientId = Id("5b7f0f5e-8f3e-4f47-9f7e-2f1d5b1c0a01");
    static readonly Uuid OtherClientId = Id("5b7f0f5e-8f3e-4f47-9f7e-2f1d5b1c0a02");
    static readonly Uuid PersonId = Id("5b7f0f5e-8f3e-4f47-9f7e-2f1d5b1c0a03");
    static readonly Uuid ColleagueId = Id("5b7f0f5e-8f3e-4f47-9f7e-2f1d5b1c0a04");
    static readonly Uuid AdvisoryEngagementId = Id("5b7f0f5e-8f3e-4f47-9f7e-2f1d5b1c0a05");
    static readonly Uuid AttestEngagementId = Id("5b7f0f5e-8f3e-4f47-9f7e-2f1d5b1c0a06");
    static readonly Uuid PersonAccountId = Id("5b7f0f5e-8f3e-4f47-9f7e-2f1d5b1c0a07");
    static readonly Uuid SecondPersonAccountId = Id("5b7f0f5e-8f3e-4f47-9f7e-2f1d5b1c0a08");
    static readonly DateOnly ExaminationPeriodStart = new(2026, 9, 22);
    static IndependenceRuleSet Rules => new(1, 12,
    [
        new("readiness_assessment", IndependenceServiceClassification.ConditionallyCompatible,
            IndependenceServiceClassification.Impairing),
        new("control_design", IndependenceServiceClassification.Impairing,
            IndependenceServiceClassification.Impairing),
        new("control_implementation", IndependenceServiceClassification.Impairing,
            IndependenceServiceClassification.Impairing),
        new("control_operation", IndependenceServiceClassification.Impairing,
            IndependenceServiceClassification.Impairing),
        new("vciso", IndependenceServiceClassification.Impairing,
            IndependenceServiceClassification.Impairing),
    ]);

    [Fact]
    public void ShouldRejectAttestAssignmentGivenSamePersonAdvisesSameClient()
    {
        // Arrange
        EngagementAssignment[] existing =
            [new(ClientId, AdvisoryEngagementId, PersonId, PersonAccountId, EngagementPractice.Advisory)];

        // Act
        var result = IndependenceCompartments.CanAssign(existing,
            new EngagementAssignment(ClientId, AttestEngagementId, PersonId, PersonAccountId,
                EngagementPractice.Attest));

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(IndependenceDecisionCode.PersonPracticeConflict, result.Code);
    }

    [Fact]
    public void ShouldRejectAttestAssignmentGivenSecondAccountForSameFirmStaffMember()
    {
        // Arrange
        EngagementAssignment[] existing =
            [new(ClientId, AdvisoryEngagementId, PersonId, PersonAccountId, EngagementPractice.Advisory)];

        // Act
        var result = IndependenceCompartments.CanAssign(existing,
            new EngagementAssignment(ClientId, AttestEngagementId, PersonId, SecondPersonAccountId,
                EngagementPractice.Attest));

        // Assert
        Assert.Equal(IndependenceDecisionCode.PersonPracticeConflict, result.Code);
    }

    [Fact]
    public void ShouldEvaluateAssignmentHistoryGivenSingleUseSequence()
    {
        // Arrange
        var assignment = new EngagementAssignment(ClientId, AdvisoryEngagementId, PersonId, PersonAccountId,
            EngagementPractice.Advisory);
        var enumerations = 0;

        IEnumerable<EngagementAssignment> History()
        {
            if (++enumerations > 1)
                throw new InvalidOperationException("The assignment history was enumerated more than once.");
            yield return assignment;
        }

        // Act
        var result = IndependenceCompartments.CanAssign(History(), assignment);

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void ShouldAllowAttestAssignmentGivenOnlyColleagueOrOtherClientAdvisory()
    {
        // Arrange
        EngagementAssignment[] existing =
        [
            new(ClientId, AdvisoryEngagementId, ColleagueId, ColleagueId, EngagementPractice.Advisory),
            new(OtherClientId, AdvisoryEngagementId, PersonId, PersonAccountId, EngagementPractice.Advisory),
        ];

        // Act
        var result = IndependenceCompartments.CanAssign(existing,
            new EngagementAssignment(ClientId, AttestEngagementId, PersonId, PersonAccountId,
                EngagementPractice.Attest));

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void ShouldAllowAttestAssignmentGivenMalformedAssignmentOwnedByAnotherClient()
    {
        // Arrange
        EngagementAssignment[] history =
            [new(OtherClientId, Uuid.Empty, PersonId, Uuid.Empty, EngagementPractice.Advisory)];

        // Act
        var result = IndependenceCompartments.CanAssign(history,
            new EngagementAssignment(ClientId, AttestEngagementId, PersonId, PersonAccountId,
                EngagementPractice.Attest));

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void ShouldBlockAdvisoryWorkingNotesGivenAttestAssignee()
    {
        // Arrange
        EngagementAssignment[] actorAssignments =
            [new(ClientId, AttestEngagementId, PersonId, PersonAccountId, EngagementPractice.Attest)];

        // Act
        var notes = IndependenceCompartments.IsBlockedByWall(ClientId, actorAssignments,
            RecordCompartment.AdvisoryWorkingNotes);
        var shared = IndependenceCompartments.IsBlockedByWall(ClientId, actorAssignments,
            RecordCompartment.Shared);

        // Assert
        Assert.True(notes);
        Assert.False(shared);
    }

    [Fact]
    public void ShouldNotBlockAdvisoryWorkingNotesGivenAdvisoryAssignee()
    {
        // Arrange
        EngagementAssignment[] actorAssignments =
            [new(ClientId, AdvisoryEngagementId, PersonId, PersonAccountId, EngagementPractice.Advisory)];

        // Act
        var blocked = IndependenceCompartments.IsBlockedByWall(ClientId, actorAssignments,
            RecordCompartment.AdvisoryWorkingNotes);

        // Assert
        Assert.False(blocked);
    }

    [Theory]
    [InlineData("control_design")]
    [InlineData("control_implementation")]
    [InlineData("control_operation")]
    public void ShouldRejectAttestAcceptanceGivenImpairingServiceWithinTwelveMonths(string service)
    {
        // Arrange
        NonattestServiceRecord[] history =
            [Record(service, ExaminationPeriodStart.AddMonths(-18), ExaminationPeriodStart.AddMonths(-11))];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, Rules, history,
            ExaminationPeriodStart, partnerEvaluationRecorded: true);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(IndependenceDecisionCode.RecentImpairingService, result.Code);
    }

    [Fact]
    public void ShouldRejectAttestAcceptanceGivenOngoingImpairingService()
    {
        // Arrange
        NonattestServiceRecord[] history =
            [Record("control_operation", ExaminationPeriodStart.AddMonths(-2), endedOn: null)];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, Rules, history,
            ExaminationPeriodStart, partnerEvaluationRecorded: true);

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void ShouldRejectAttestAcceptanceGivenImpairingServiceEndingAtLookBackBoundary()
    {
        // Arrange
        NonattestServiceRecord[] history =
            [Record("control_design", ExaminationPeriodStart.AddMonths(-18),
                ExaminationPeriodStart.AddMonths(-12))];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, Rules, history,
            ExaminationPeriodStart, partnerEvaluationRecorded: false);

        // Assert
        Assert.Equal(IndependenceDecisionCode.RecentImpairingService, result.Code);
    }

    [Fact]
    public void ShouldAllowAttestAcceptanceGivenImpairingServiceEndedMoreThanTwelveMonthsAgo()
    {
        // Arrange
        NonattestServiceRecord[] history =
            [Record("control_design", ExaminationPeriodStart.AddMonths(-18),
                ExaminationPeriodStart.AddMonths(-12).AddDays(-1))];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, Rules, history,
            ExaminationPeriodStart, partnerEvaluationRecorded: false);

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void ShouldRequirePartnerEvaluationGivenRecentReadinessAssessmentOnly()
    {
        // Arrange
        NonattestServiceRecord[] history =
            [Record("readiness_assessment", ExaminationPeriodStart.AddMonths(-3),
                ExaminationPeriodStart.AddMonths(-2))];

        // Act
        var withoutEvaluation = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, Rules, history,
            ExaminationPeriodStart,
            partnerEvaluationRecorded: false);
        var withEvaluation = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, Rules, history,
            ExaminationPeriodStart,
            partnerEvaluationRecorded: true);

        // Assert
        Assert.False(withoutEvaluation.IsAllowed);
        Assert.Equal(IndependenceDecisionCode.PartnerEvaluationRequired, withoutEvaluation.Code);
        Assert.True(withEvaluation.IsAllowed);
        Assert.Equal(IndependenceEvaluationOutcome.ConditionallyCompatible, withEvaluation.EvaluationOutcome);
    }

    [Fact]
    public void ShouldIgnoreOtherClientAdvisoryGivenAttestAcceptance()
    {
        // Arrange
        NonattestServiceRecord[] history =
            [Record("control_design", ExaminationPeriodStart.AddMonths(-2), ExaminationPeriodStart.AddMonths(-1),
                OtherClientId)];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, Rules, history,
            ExaminationPeriodStart,
            partnerEvaluationRecorded: false);

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void ShouldAllowAttestAcceptanceGivenMalformedHistoryFromAnotherClient()
    {
        // Arrange
        NonattestServiceRecord[] history =
        [
            new(OtherClientId, Uuid.Empty, "control_design", ExaminationPeriodStart.AddMonths(-2),
                ExaminationPeriodStart.AddMonths(-1), [PersonId], false),
        ];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, Rules, history,
            ExaminationPeriodStart, partnerEvaluationRecorded: false);

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void ShouldIgnoreServiceStartingAfterExaminationPeriodGivenAttestAcceptance()
    {
        // Arrange
        NonattestServiceRecord[] history =
            [Record("control_design", ExaminationPeriodStart.AddDays(1), ExaminationPeriodStart.AddMonths(1))];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, Rules, history,
            ExaminationPeriodStart,
            partnerEvaluationRecorded: false);

        // Assert
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void ShouldUseVersionedClassificationGivenServiceTypeNotKnownToCode()
    {
        // Arrange
        var ruleSet = new IndependenceRuleSet(7, 12,
            [new IndependenceServiceRule("custom_service", IndependenceServiceClassification.Impairing,
                IndependenceServiceClassification.Impairing)]);
        NonattestServiceRecord[] history =
            [Record("custom_service", ExaminationPeriodStart.AddMonths(-6), ExaminationPeriodStart.AddMonths(-1))];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, ruleSet, history,
            ExaminationPeriodStart, partnerEvaluationRecorded: false);

        // Assert
        Assert.Equal(IndependenceDecisionCode.RecentImpairingService, result.Code);
    }

    [Fact]
    public void ShouldRejectUnclassifiedServiceGivenActiveRuleSet()
    {
        // Arrange
        NonattestServiceRecord[] history =
            [Record("unclassified_service", ExaminationPeriodStart.AddMonths(-2), endedOn: null)];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, Rules, history,
            ExaminationPeriodStart, partnerEvaluationRecorded: true);

        // Assert
        Assert.Equal(IndependenceDecisionCode.ServiceNotClassified, result.Code);
    }

    [Fact]
    public void ShouldRejectInvalidRuleSetGivenAttestAcceptance()
    {
        // Arrange
        var invalidRuleSet = new IndependenceRuleSet(0, 12, []);

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, invalidRuleSet, [],
            ExaminationPeriodStart, partnerEvaluationRecorded: false);

        // Assert
        Assert.Equal(IndependenceDecisionCode.RuleSetInvalid, result.Code);
    }

    [Fact]
    public void ShouldRejectMissingTenantGivenAttestAcceptance()
    {
        // Arrange
        var tenantId = Uuid.Empty;

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(tenantId, Rules, [],
            ExaminationPeriodStart, partnerEvaluationRecorded: true);

        // Assert
        Assert.Equal(IndependenceDecisionCode.EvaluationInputInvalid, result.Code);
    }

    [Fact]
    public void ShouldRejectMalformedServiceHistoryGivenAttestAcceptance()
    {
        // Arrange
        NonattestServiceRecord[] history =
        [
            new(ClientId, Uuid.Empty, "readiness_assessment", ExaminationPeriodStart.AddMonths(-3),
                ExaminationPeriodStart.AddMonths(-2), [PersonId], false),
        ];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, Rules, history,
            ExaminationPeriodStart, partnerEvaluationRecorded: true);

        // Assert
        Assert.Equal(IndependenceDecisionCode.ServiceHistoryInvalid, result.Code);
    }

    [Fact]
    public void ShouldRejectDuplicateServiceClassificationsGivenAttestAcceptance()
    {
        // Arrange
        var invalidRuleSet = new IndependenceRuleSet(2, 12,
        [
            new("service", IndependenceServiceClassification.Compatible,
                IndependenceServiceClassification.Compatible),
            new("service", IndependenceServiceClassification.Impairing,
                IndependenceServiceClassification.Impairing),
        ]);

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, invalidRuleSet, [],
            ExaminationPeriodStart, partnerEvaluationRecorded: true);

        // Assert
        Assert.Equal(IndependenceDecisionCode.RuleSetInvalid, result.Code);
    }

    [Fact]
    public void ShouldRejectUnrepresentableLookBackGivenAttestAcceptance()
    {
        // Arrange
        var ruleSet = new IndependenceRuleSet(9, int.MaxValue, []);

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, ruleSet, [],
            ExaminationPeriodStart, partnerEvaluationRecorded: false);

        // Assert
        Assert.Equal(IndependenceDecisionCode.RuleSetInvalid, result.Code);
    }

    [Fact]
    public void ShouldRejectUnrepresentableExaminationPeriodGivenAttestAcceptance()
    {
        // Arrange
        var periodStart = new DateOnly(1, 1, 2);

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, Rules, [],
            periodStart, partnerEvaluationRecorded: false);

        // Assert
        Assert.Equal(IndependenceDecisionCode.EvaluationInputInvalid, result.Code);
    }

    [Fact]
    public void ShouldUseManagementFunctionClassificationGivenRecordedService()
    {
        // Arrange
        var ruleSet = new IndependenceRuleSet(8, 12,
        [
            new("readiness_assessment", IndependenceServiceClassification.ConditionallyCompatible,
                IndependenceServiceClassification.Impairing),
        ]);
        NonattestServiceRecord[] history =
            [Record("readiness_assessment", ExaminationPeriodStart.AddMonths(-3), endedOn: null,
                involvedManagementFunctions: true)];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, ruleSet, history,
            ExaminationPeriodStart, partnerEvaluationRecorded: true);

        // Assert
        Assert.Equal(IndependenceDecisionCode.RecentImpairingService, result.Code);
    }

    [Fact]
    public void ShouldRetainRuleVersionAndConsideredServicesGivenAttestEvaluation()
    {
        // Arrange
        var service = Record("readiness_assessment", ExaminationPeriodStart.AddMonths(-6),
            ExaminationPeriodStart.AddMonths(-1));
        var history = new[] { service };

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, Rules, history,
            ExaminationPeriodStart, partnerEvaluationRecorded: false);

        // Assert
        Assert.Equal(1, result.RuleSetVersion);
        Assert.False(result.PartnerEvaluationRecorded);
        Assert.Equal(IndependenceEvaluationOutcome.ConditionallyCompatible, result.EvaluationOutcome);
        Assert.Equal(ExaminationPeriodStart, result.ExaminationPeriodStart);
        Assert.Equal(Rules.LookBackMonths, result.LookBackMonths);
        var considered = Assert.Single(result.ConsideredServices);
        Assert.Equal(service.ServiceEngagementId, considered.Service.ServiceEngagementId);
        Assert.Equal(IndependenceServiceClassification.ConditionallyCompatible, considered.Classification);
    }

    static NonattestServiceRecord Record(string serviceType, DateOnly startedOn, DateOnly? endedOn,
        Uuid? clientTenantId = null, bool involvedManagementFunctions = false) =>
        new(clientTenantId ?? ClientId, Uuid.CreateVersion4(), serviceType,
            startedOn, endedOn, [PersonId], involvedManagementFunctions);

    static Uuid Id(string value) => Uuid.Parse(value, CultureInfo.InvariantCulture);
}
