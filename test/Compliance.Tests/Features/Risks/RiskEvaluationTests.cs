using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskEvaluationTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid AssessorId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldDeriveScoreAndKeepAttributableHistoryGivenInherentAndTarget()
    {
        // Arrange
        var method = Method(12);
        var evaluation = New();
        var inherentId = Uuid.CreateVersion4();

        // Act
        var inherent = evaluation.RecordAssessment(ProgramId, 0, inherentId, method, "inherent",
            4, 5, "Provider outage is likely", AssessorId, "Assessor", Now);
        var replay = evaluation.RecordAssessment(ProgramId, 0, inherentId, method, "inherent",
            4, 5, "Provider outage is likely", AssessorId, "Assessor", Now);
        var target = evaluation.RecordAssessment(ProgramId, 1, Uuid.CreateVersion4(), method,
            "target", 2, 3, "After failover", AssessorId, "Assessor", Now.AddMinutes(1));

        // Assert
        Assert.Null(inherent);
        Assert.Null(replay);
        Assert.Null(target);
        Assert.Equal(2, evaluation.Revision);
        var recorded = Assert.IsType<RiskAssessmentRecorded>(
            new AggregateScenario<RiskEvaluation>(evaluation).PendingEvents[0]);
        Assert.Equal(20, recorded.Assessment.Score);
        Assert.Equal(ActorReference.ForMember(AssessorId, "Assessor"), recorded.Assessment.Assessor);
        Assert.Equal(method.MethodVersionId, recorded.Assessment.MethodVersionId);
        Assert.Equal(6, evaluation.Latest("target")!.Score);
    }

    [Fact]
    public void ShouldRejectAssessmentGivenInvalidPhaseScaleOrOrdering()
    {
        // Arrange
        var method = Method(12);
        var evaluation = New();

        // Act
        var residualFirst = evaluation.RecordAssessment(ProgramId, 0, Uuid.CreateVersion4(),
            method, "residual", 2, 2, "Too early", AssessorId, "Assessor", Now);
        var badPhase = evaluation.RecordAssessment(ProgramId, 0, Uuid.CreateVersion4(),
            method, "gross", 2, 2, "Unknown", AssessorId, "Assessor", Now);
        var badScale = evaluation.RecordAssessment(ProgramId, 0, Uuid.CreateVersion4(),
            method, "inherent", 6, 2, "Off scale", AssessorId, "Assessor", Now);
        var otherProgram = evaluation.RecordAssessment(ProgramId, 0, Uuid.CreateVersion4(),
            method with { ProgramId = Uuid.CreateVersion4() }, "inherent", 2, 2, "Wrong",
            AssessorId, "Assessor", Now);
        var stale = evaluation.RecordAssessment(ProgramId, 3, Uuid.CreateVersion4(),
            method, "inherent", 2, 2, "Stale", AssessorId, "Assessor", Now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, residualFirst!.Code);
        Assert.Equal(CommandFailureCode.InvalidContent, badPhase!.Code);
        Assert.Equal(CommandFailureCode.InvalidContent, badScale!.Code);
        Assert.Equal(CommandFailureCode.InvalidContent, otherProgram!.Code);
        Assert.Equal(CommandFailureCode.VersionConflict, stale!.Code);
        Assert.Equal(0, evaluation.Revision);
    }

    [Fact]
    public void ShouldRequireNonMitigateTreatmentGivenResidualWithoutControlTreatment()
    {
        // Arrange
        var method = Method(12);
        var evaluation = New();
        Assert.Null(Inherent(evaluation, method));
        Assert.Null(evaluation.ChooseTreatment(ProgramId, 1, "mitigate", "Add failover",
            AssessorId, "Assessor", Now));

        // Act
        var residual = evaluation.RecordAssessment(ProgramId, 2, Uuid.CreateVersion4(),
            method, "residual", 2, 2, "After failover", AssessorId, "Assessor", Now);
        var badKind = evaluation.ChooseTreatment(ProgramId, 2, "ignore", "No",
            AssessorId, "Assessor", Now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, residual!.Code);
        Assert.Equal(CommandFailureCode.InvalidContent, badKind!.Code);
        Assert.Equal("mitigate", evaluation.Treatment!.Kind);
    }

    [Fact]
    public void ShouldAcceptWithinAppetiteGivenComplianceLeadAndTwelveMonthExpiry()
    {
        // Arrange
        var method = Method(12);
        var (evaluation, residualId) = ReadyForAcceptance(method, likelihood: 3, impact: 4);

        // Act
        var accepted = evaluation.Accept(ProgramId, 3, Uuid.CreateVersion4(), residualId,
            method, "compliance_lead", true, Now.AddMonths(12), "Within appetite",
            ApproverId, "Lead", Now);

        // Assert
        Assert.Null(accepted);
        var acceptance = Assert.IsType<RiskAccepted>(
            new AggregateScenario<RiskEvaluation>(evaluation).PendingEvents[^1]).Acceptance;
        Assert.Equal("compliance_lead", acceptance.ApproverAuthority);
        Assert.Equal(ApproverId, acceptance.ApproverMemberId);
        Assert.Equal(Now.AddMonths(12), acceptance.ExpiresAt);
        Assert.Equal(12, acceptance.ResidualScore);
    }

    [Theory]
    [InlineData(12, 4, 4, "compliance_lead", true, CommandFailureCode.ActorProhibited)]
    [InlineData(null, 1, 1, "compliance_lead", true, CommandFailureCode.ActorProhibited)]
    [InlineData(12, 1, 1, "compliance_lead", false, CommandFailureCode.ActorProhibited)]
    [InlineData(12, 4, 4, "executive", false, CommandFailureCode.ActorProhibited)]
    [InlineData(12, 1, 1, "owner", true, CommandFailureCode.InvalidContent)]
    public void ShouldDenyAcceptanceGivenMissingAuthority(int? appetite, int likelihood,
        int impact, string authority, bool held, CommandFailureCode expected)
    {
        // Arrange
        var method = Method(appetite);
        var (evaluation, residualId) = ReadyForAcceptance(method, likelihood, impact);

        // Act
        var failure = evaluation.Accept(ProgramId, 3, Uuid.CreateVersion4(), residualId, method,
            authority, held, Now.AddMonths(6), "Accept", ApproverId, "Approver", Now);

        // Assert
        Assert.Equal(expected, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(3, evaluation.Revision);
    }

    [Fact]
    public void ShouldAcceptAboveAppetiteGivenExecutiveAuthority()
    {
        // Arrange
        var method = Method(null);
        var (evaluation, residualId) = ReadyForAcceptance(method, 5, 5);

        // Act
        var failure = evaluation.Accept(ProgramId, 3, Uuid.CreateVersion4(), residualId, method,
            "executive", true, Now.AddMonths(3), "Board accepts", ApproverId, "CEO", Now);

        // Assert
        Assert.Null(failure);
        Assert.Equal(4, evaluation.Revision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public void ShouldDenyAcceptanceGivenExpiryOutsideTwelveMonths(int days)
    {
        // Arrange
        var method = Method(12);
        var (evaluation, residualId) = ReadyForAcceptance(method, 1, 1);

        // Act
        var failure = evaluation.Accept(ProgramId, 3, Uuid.CreateVersion4(), residualId, method,
            "executive", true, Now.AddDays(days), "Accept", ApproverId, "CEO", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
    }

    [Fact]
    public void ShouldDenyAcceptanceGivenApproverAssessedResidualOrTreatmentNotAccept()
    {
        // Arrange
        var method = Method(12);
        var (evaluation, residualId) = ReadyForAcceptance(method, 1, 1);
        var transfer = New();
        Assert.Null(Inherent(transfer, method));
        Assert.Null(transfer.ChooseTreatment(ProgramId, 1, "transfer", "Insured",
            AssessorId, "Assessor", Now));
        var transferResidual = Uuid.CreateVersion4();
        Assert.Null(transfer.RecordAssessment(ProgramId, 2, transferResidual, method,
            "residual", 1, 1, "Insured", AssessorId, "Assessor", Now));

        // Act
        var selfApproval = evaluation.Accept(ProgramId, 3, Uuid.CreateVersion4(), residualId,
            method, "executive", true, Now.AddMonths(1), "Mine", AssessorId, "Assessor", Now);
        var notAccept = transfer.Accept(ProgramId, 3, Uuid.CreateVersion4(), transferResidual,
            method, "executive", true, Now.AddMonths(1), "Accept", ApproverId, "CEO", Now);
        var unknownResidual = evaluation.Accept(ProgramId, 3, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), method, "executive", true, Now.AddMonths(1), "Accept",
            ApproverId, "CEO", Now);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, selfApproval!.Code);
        Assert.Equal(CommandFailureCode.StateConflict, notAccept!.Code);
        Assert.Equal(CommandFailureCode.StateConflict, unknownResidual!.Code);
    }

    [Fact]
    public void ShouldReportReassessmentDueGivenExpiredAcceptance()
    {
        // Arrange
        var method = Method(12);
        var (evaluation, residualId) = ReadyForAcceptance(method, 1, 1);
        Assert.Null(evaluation.Accept(ProgramId, 3, Uuid.CreateVersion4(), residualId, method,
            "compliance_lead", true, Now.AddMonths(1), "Accept", ApproverId, "Lead", Now));
        var view = evaluation.ToView();

        // Act
        var active = RiskEvaluationStatus.AsOf(view, Now.AddDays(1));
        var expired = RiskEvaluationStatus.AsOf(view, Now.AddMonths(1));
        var unassessed = RiskEvaluationStatus.AsOf(New().ToView(), Now);

        // Assert
        Assert.Equal("accepted", active.Status);
        Assert.Equal("active", Assert.Single(active.Acceptances).Status);
        Assert.Equal("reassessment_due", expired.Status);
        Assert.Equal("expired", Assert.Single(expired.Acceptances).Status);
        Assert.Equal("unassessed", unassessed.Status);
        Assert.Equal(Now.AddYears(1), active.ReassessmentDueAt);
    }

    [Theory]
    [InlineData("compliance_lead", "risk.accept.compliance_lead")]
    [InlineData("executive", "risk.accept.executive")]
    [InlineData("owner", null)]
    public void ShouldRequireDistinctGrantGivenClaimedAcceptanceAuthority(string authority,
        string? expected)
    {
        // Arrange
        var claimed = authority;

        // Act
        var permission = RbacPermissions.RiskAcceptanceFor(claimed);

        // Assert
        Assert.Equal(expected, permission);
    }

    static RiskEvaluation New() => new(TenantId, Uuid.CreateVersion4());

    static CommandFailure? Inherent(RiskEvaluation evaluation, RiskMethodVersionView method) =>
        evaluation.RecordAssessment(ProgramId, 0, Uuid.CreateVersion4(), method, "inherent",
            5, 5, "Severe", AssessorId, "Assessor", Now);

    static (RiskEvaluation Evaluation, Uuid ResidualId) ReadyForAcceptance(
        RiskMethodVersionView method, int likelihood, int impact)
    {
        var evaluation = New();
        Assert.Null(Inherent(evaluation, method));
        Assert.Null(evaluation.ChooseTreatment(ProgramId, 1, "accept", "Cost exceeds benefit",
            AssessorId, "Assessor", Now));
        var residualId = Uuid.CreateVersion4();
        Assert.Null(evaluation.RecordAssessment(ProgramId, 2, residualId, method, "residual",
            likelihood, impact, "As accepted", AssessorId, "Assessor", Now));
        return (evaluation, residualId);
    }

    static RiskMethodVersionView Method(int? appetite)
    {
        var method = new RiskMethod(TenantId, ProgramId);
        Assert.Null(method.Publish(ProgramId, 0, RiskMethodTests.Scale(),
            RiskMethodTests.Scale(), appetite, ActorReference.ForMember(ApproverId, "Lead"),
            Now));
        return method.Current!;
    }
}
