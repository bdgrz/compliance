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
    static readonly DateOnly AcceptedOn = new(2026, 9, 22);

    [Fact]
    public void ShouldRejectAttestAssignmentGivenSamePersonAdvisesSameClient()
    {
        // Arrange
        EngagementAssignment[] existing =
            [new(ClientId, AdvisoryEngagementId, PersonId, EngagementPractice.Advisory)];

        // Act
        var result = IndependenceCompartments.CanAssign(existing,
            new EngagementAssignment(ClientId, AttestEngagementId, PersonId, EngagementPractice.Attest));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
    }

    [Fact]
    public void ShouldAllowAttestAssignmentGivenOnlyColleagueOrOtherClientAdvisory()
    {
        // Arrange
        EngagementAssignment[] existing =
        [
            new(ClientId, AdvisoryEngagementId, ColleagueId, EngagementPractice.Advisory),
            new(OtherClientId, AdvisoryEngagementId, PersonId, EngagementPractice.Advisory),
        ];

        // Act
        var result = IndependenceCompartments.CanAssign(existing,
            new EngagementAssignment(ClientId, AttestEngagementId, PersonId, EngagementPractice.Attest));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ShouldBlockAdvisoryWorkingNotesGivenAttestAssignee()
    {
        // Arrange
        EngagementAssignment[] actorAssignments =
            [new(ClientId, AttestEngagementId, PersonId, EngagementPractice.Attest)];

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
            [new(ClientId, AdvisoryEngagementId, PersonId, EngagementPractice.Advisory)];

        // Act
        var blocked = IndependenceCompartments.IsBlockedByWall(ClientId, actorAssignments,
            RecordCompartment.AdvisoryWorkingNotes);

        // Assert
        Assert.False(blocked);
    }

    [Theory]
    [InlineData(AdvisoryService.ControlDesign)]
    [InlineData(AdvisoryService.ControlImplementation)]
    [InlineData(AdvisoryService.ControlOperation)]
    public void ShouldRejectAttestAcceptanceGivenImpairingServiceWithinTwelveMonths(AdvisoryService service)
    {
        // Arrange
        AdvisoryEngagementRecord[] history = [new(ClientId, service, AcceptedOn.AddMonths(-11))];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, history, AcceptedOn,
            partnerEvaluationRecorded: true);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
    }

    [Fact]
    public void ShouldRejectAttestAcceptanceGivenOngoingImpairingService()
    {
        // Arrange
        AdvisoryEngagementRecord[] history = [new(ClientId, AdvisoryService.ControlOperation, EndedOn: null)];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, history, AcceptedOn,
            partnerEvaluationRecorded: true);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ShouldAllowAttestAcceptanceGivenImpairingServiceEndedMoreThanTwelveMonthsAgo()
    {
        // Arrange
        AdvisoryEngagementRecord[] history =
            [new(ClientId, AdvisoryService.ControlDesign, AcceptedOn.AddMonths(-12).AddDays(-1))];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, history, AcceptedOn,
            partnerEvaluationRecorded: false);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ShouldRequirePartnerEvaluationGivenRecentReadinessAssessmentOnly()
    {
        // Arrange
        AdvisoryEngagementRecord[] history = [new(ClientId, AdvisoryService.ReadinessAssessment, AcceptedOn.AddMonths(-2))];

        // Act
        var withoutEvaluation = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, history, AcceptedOn,
            partnerEvaluationRecorded: false);
        var withEvaluation = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, history, AcceptedOn,
            partnerEvaluationRecorded: true);

        // Assert
        Assert.False(withoutEvaluation.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, withoutEvaluation.Error!.Kind);
        Assert.True(withEvaluation.IsSuccess);
    }

    [Fact]
    public void ShouldIgnoreOtherClientAdvisoryGivenAttestAcceptance()
    {
        // Arrange
        AdvisoryEngagementRecord[] history =
            [new(OtherClientId, AdvisoryService.ControlDesign, AcceptedOn.AddMonths(-1))];

        // Act
        var result = IndependenceCompartments.CanAcceptAttestEngagement(ClientId, history, AcceptedOn,
            partnerEvaluationRecorded: false);

        // Assert
        Assert.True(result.IsSuccess);
    }

    static Uuid Id(string value) => Uuid.Parse(value, CultureInfo.InvariantCulture);
}
