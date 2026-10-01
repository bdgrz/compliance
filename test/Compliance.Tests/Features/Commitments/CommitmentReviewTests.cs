using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

public sealed class CommitmentReviewTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ServiceId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly January = new(2027, 1, 1);

    static CommitmentDraft Draft(string kind = "service_commitment")
    {
        var id = CommitmentDraft.IdFor(TenantId, ProgramId, kind, "SC-01");
        var draft = new CommitmentDraft(TenantId, id);
        Assert.True(draft.Create(ProgramId, Uuid.CreateVersion4(), ServiceId, kind, "SC-01",
            "Statement", "Context", "Contract 4.1", AuthorId, "Author", Now).IsSuccess);
        return draft;
    }

    static CommandFailure? Accept(CommitmentDraft draft, long revision, Uuid reviewer,
        DateOnly effectiveFrom, SeparationOfDutiesWaiver? waiver = null,
        string interpretation = "supported")
    {
        var reviewId = Uuid.CreateVersion4();
        var review = draft.Review(ProgramId, revision, reviewId, "accept", "Security lead",
            "applicable", interpretation, "Read against MSA 4.1", "Verified with owner",
            reviewer, "Reviewer", Now.AddMinutes(5), waiver,
            draft.SourceReference, "Signed MSA v3, section 4.1");
        return review ?? draft.Approve(ProgramId, revision, Uuid.CreateVersion4(), reviewId,
            effectiveFrom, "Approved for the engagement", "digest", ApproverId, "Approver",
            Now.AddMinutes(6));
    }

    [Fact]
    public void ShouldCreateImmutableEffectiveVersionGivenIndependentAcceptedReview()
    {
        // Arrange
        var draft = Draft();

        // Act
        var failure = Accept(draft, 1, ReviewerId, January);

        // Assert
        Assert.Null(failure);
        Assert.Equal(1, draft.EffectiveVersionCount);
        Assert.Equal(1, draft.EffectiveVersionOn(January));
        Assert.Null(draft.EffectiveVersionOn(January.AddDays(-1)));
        var events = new AggregateScenario<CommitmentDraft>(draft).PendingEvents;
        var reviewed = Assert.IsType<CommitmentReviewed>(events[^2]);
        Assert.Null(reviewed.Version);
        Assert.Equal("Security lead", reviewed.OwnerReference);
        Assert.Equal(ReviewerId.ToString(), reviewed.Actor.Id);
        var approved = Assert.IsType<CommitmentApproved>(events[^1]);
        Assert.Equal(1, approved.Version);
        Assert.Equal(reviewed.DecisionId, approved.AcceptedReviewDecisionId);
    }

    [Fact]
    public void ShouldRejectReviewGivenReviewerAuthoredPendingRevision()
    {
        // Arrange
        var draft = Draft();
        Assert.Null(draft.Revise(ProgramId, 1, "Revised", "Context", "Contract 4.1",
            ReviewerId, "Reviewer", Now.AddMinutes(1)));

        // Act
        var selfReview = Accept(draft, 2, ReviewerId, January);
        var originalAuthor = Accept(draft, 2, AuthorId, January);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(selfReview).Code);
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(originalAuthor).Code);
        Assert.Equal(0, draft.EffectiveVersionCount);
    }

    [Fact]
    public void ShouldAllowAuthorReviewGivenApprovedExactWaiver()
    {
        // Arrange
        var draft = Draft();
        var scope = new SeparationOfDutiesWaiverScope(SeparationOfDutiesRecordTypes.Commitment,
            draft.Id, draft.Id, 1, SeparationOfDutiesActions.Review);
        var wrongRevision = CreateWaiver(scope with { Revision = 2 }, AuthorId);
        var valid = CreateWaiver(scope, AuthorId);

        // Act
        var denied = Accept(draft, 1, AuthorId, January, wrongRevision);
        var allowed = Accept(draft, 1, AuthorId, January, valid);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(denied).Code);
        Assert.Null(allowed);
        Assert.Equal(valid.Id, Assert.IsType<CommitmentReviewed>(
            new AggregateScenario<CommitmentDraft>(draft).PendingEvents[^2])
            .SeparationOfDutiesWaiverId);
    }

    [Theory]
    [InlineData("", "applicable", "supported")]
    [InlineData("Owner", "maybe", "supported")]
    [InlineData("Owner", "applicable", "assumed")]
    public void ShouldRejectAcceptanceGivenUnverifiedOwnerApplicabilityOrInterpretation(
        string owner, string applicability, string interpretation)
    {
        // Arrange
        var draft = Draft();

        // Act
        var failure = draft.Review(ProgramId, 1, Uuid.CreateVersion4(), "accept", owner,
            applicability, interpretation, null, "Rationale", ReviewerId, "Reviewer", Now,
            sourceVerifiedReference: "Contract 4.1", sourceEvidence: "Signed MSA");

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
    }

    [Fact]
    public void ShouldKeepUnsupportedInterpretationVisibleGivenAcceptedReview()
    {
        // Arrange
        var draft = Draft();

        // Act
        var failure = Accept(draft, 1, ReviewerId, January, interpretation: "unsupported");

        // Assert
        Assert.Null(failure);
        Assert.Equal("unsupported", Assert.IsType<CommitmentReviewed>(
            new AggregateScenario<CommitmentDraft>(draft).PendingEvents[^2]).Interpretation);
    }

    [Theory]
    [InlineData("service_commitment", "service_organization", true)]
    [InlineData("system_requirement", "service_organization", true)]
    [InlineData("user_entity_responsibility", "user_entity", false)]
    [InlineData("subservice_responsibility", "subservice_organization", false)]
    public void ShouldNeverTreatCarveOutResponsibilityAsInternalGivenKind(string kind,
        string performedBy, bool internallyPerformed)
    {
        // Arrange
        var normalized = CommitmentDraft.NormalizeKind(kind);

        // Act
        var party = CommitmentDraft.PerformedBy(normalized);

        // Assert
        Assert.Equal(performedBy, party);
        Assert.Equal(internallyPerformed, CommitmentDraft.IsInternallyPerformed(normalized));
    }

    [Fact]
    public void ShouldRequireLaterEffectiveDateAndNewRevisionGivenSuccessorVersion()
    {
        // Arrange
        var draft = Draft();
        Assert.Null(Accept(draft, 1, ReviewerId, January));

        // Act
        var sameRevision = Accept(draft, 1, ReviewerId, January.AddDays(1));
        Assert.Null(draft.Revise(ProgramId, 1, "Successor", "Context", "Contract 4.2",
            AuthorId, "Author", Now.AddMinutes(6)));
        var notLater = Accept(draft, 2, ReviewerId, January);
        var successor = Accept(draft, 2, ReviewerId, January.AddMonths(1));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(sameRevision).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(notLater).Code);
        Assert.Null(successor);
        Assert.Equal(2, draft.EffectiveVersionCount);
        Assert.Equal(1, draft.EffectiveVersionOn(January.AddDays(10)));
        Assert.Equal(2, draft.EffectiveVersionOn(January.AddMonths(2)));
    }

    [Fact]
    public void ShouldRecordRequestChangesWithoutVersionGivenRationale()
    {
        // Arrange
        var draft = Draft();

        // Act
        var missingRationale = draft.Review(ProgramId, 1, Uuid.CreateVersion4(),
            "request_changes", null, null, null, null, " ", ReviewerId, "Reviewer", Now);
        var failure = draft.Review(ProgramId, 1, Uuid.CreateVersion4(), "request_changes",
            null, null, null, null, "Owner unclear", ReviewerId, "Reviewer", Now);
        var stale = draft.Review(ProgramId, 3, Uuid.CreateVersion4(), "request_changes",
            null, null, null, null, "Owner unclear", ReviewerId, "Reviewer", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(missingRationale).Code);
        Assert.Null(failure);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(0, draft.EffectiveVersionCount);
        Assert.Null(Assert.IsType<CommitmentReviewed>(
            new AggregateScenario<CommitmentDraft>(draft).PendingEvents[^1]).Version);
    }

    static SeparationOfDutiesWaiver CreateWaiver(SeparationOfDutiesWaiverScope scope,
        Uuid beneficiary)
    {
        var waiver = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(scope, beneficiary, Uuid.CreateVersion4(), "Requester",
            "No other qualified reviewer is available.", Now, Now.AddDays(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Approver", Now.AddMinutes(1)));
        return waiver;
    }
}
