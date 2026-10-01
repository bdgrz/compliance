using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

public sealed class CommitmentApprovalTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ServiceId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly January = new(2027, 1, 1);

    static CommitmentDraft Draft()
    {
        var id = CommitmentDraft.IdFor(TenantId, ProgramId, "service_commitment", "SC-01");
        var draft = new CommitmentDraft(TenantId, id);
        Assert.True(draft.Create(ProgramId, Uuid.CreateVersion4(), ServiceId,
            "service_commitment", "SC-01", "Statement", "Context", "MSA 4.1", AuthorId,
            "Author", Now).IsSuccess);
        return draft;
    }

    static CommandFailure? Review(CommitmentDraft draft, long revision, Uuid decisionId,
        Uuid reviewer, string? verifiedReference = "MSA 4.1",
        SeparationOfDutiesWaiver? waiver = null) =>
        draft.Review(ProgramId, revision, decisionId, "accept", "Security lead", "applicable",
            "supported", null, "Verified", reviewer, "Reviewer", Now.AddMinutes(5), waiver,
            verifiedReference, "Signed MSA v3, section 4.1");

    static CommandFailure? Approve(CommitmentDraft draft, long revision, Uuid reviewId,
        Uuid approver, SeparationOfDutiesWaiver? waiver = null) =>
        draft.Approve(ProgramId, revision, Uuid.CreateVersion4(), reviewId, January,
            "Approved", "digest", approver, "Approver", Now.AddMinutes(10), waiver);

    static ResponsibilityScope Scope(CommitmentDraft draft, long revision) =>
        new(SeparationOfDutiesRecordTypes.Commitment, draft.Id, draft.Id, revision);

    [Fact]
    public void ShouldRejectApprovalGivenNoAcceptedReviewOfRevision()
    {
        // Arrange
        var draft = Draft();

        // Act
        var failure = Approve(draft, 1, Uuid.CreateVersion4(), ApproverId);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, draft.EffectiveVersionCount);
    }

    [Fact]
    public void ShouldRejectApprovalGivenApproverIsAcceptedReviewerWithoutWaiver()
    {
        // Arrange
        var draft = Draft();
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(Review(draft, 1, reviewId, ReviewerId));
        var waiver = CreateWaiver(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Commitment, draft.Id, draft.Id, 1,
            SeparationOfDutiesActions.Approve), ReviewerId);

        // Act
        var denied = Approve(draft, 1, reviewId, ReviewerId);
        var waived = Approve(draft, 1, reviewId, ReviewerId, waiver);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(denied).Code);
        Assert.Null(waived);
        Assert.Equal(waiver.Id, Assert.IsType<CommitmentApproved>(
            new AggregateScenario<CommitmentDraft>(draft).PendingEvents[^1])
            .SeparationOfDutiesWaiverId);
    }

    [Fact]
    public void ShouldRejectApprovalGivenApproverAuthoredPendingRevision()
    {
        // Arrange
        var draft = Draft();
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(Review(draft, 1, reviewId, ReviewerId));

        // Act
        var failure = Approve(draft, 1, reviewId, AuthorId);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(failure).Code);
    }

    [Fact]
    public void ShouldRequireFreshReviewGivenRevisionAfterAcceptedReview()
    {
        // Arrange
        var draft = Draft();
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(Review(draft, 1, reviewId, ReviewerId));
        Assert.Null(draft.Revise(ProgramId, 1, "Changed", "Context", "MSA 4.1", AuthorId,
            "Author", Now.AddMinutes(7)));

        // Act
        var stale = Approve(draft, 1, reviewId, ApproverId);
        var unreviewed = Approve(draft, 2, reviewId, ApproverId);

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(unreviewed).Code);
    }

    [Fact]
    public void ShouldRejectAcceptanceGivenSourceVerificationOfDifferentReference()
    {
        // Arrange
        var draft = Draft();

        // Act
        var mismatch = Review(draft, 1, Uuid.CreateVersion4(), ReviewerId, "MSA 9.9");
        var missing = Review(draft, 1, Uuid.CreateVersion4(), ReviewerId, null);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(mismatch).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(missing).Code);
    }

    [Fact]
    public void ShouldRecordVerifiedSourceProvenanceGivenAcceptedReview()
    {
        // Arrange
        var draft = Draft();

        // Act
        var failure = Review(draft, 1, Uuid.CreateVersion4(), ReviewerId);

        // Assert
        Assert.Null(failure);
        var reviewed = Assert.IsType<CommitmentReviewed>(
            new AggregateScenario<CommitmentDraft>(draft).PendingEvents[^1]);
        Assert.Equal("verified", reviewed.SourceVerification);
        Assert.Equal("MSA 4.1", reviewed.SourceVerifiedReference);
        Assert.Equal("Signed MSA v3, section 4.1", reviewed.SourceEvidence);
    }

    [Fact]
    public void ShouldRestrictReviewToAssignedReviewerGivenActiveAssignment()
    {
        // Arrange
        var draft = Draft();
        var other = Uuid.CreateVersion4();
        Assert.Null(draft.AssignResponsibility(Scope(draft, 1), Uuid.CreateVersion4(), other,
            ResponsibilityType.AssignedReviewer, AuthorId, "Author", Now, Now, null, []));

        // Act
        var unassigned = Review(draft, 1, Uuid.CreateVersion4(), ReviewerId);
        var assigned = Review(draft, 1, Uuid.CreateVersion4(), other);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(unassigned).Code);
        Assert.Null(assigned);
    }

    [Fact]
    public void ShouldRestrictApprovalToAssignedApproverGivenActiveAssignment()
    {
        // Arrange
        var draft = Draft();
        var reviewId = Uuid.CreateVersion4();
        var assignedApprover = Uuid.CreateVersion4();
        Assert.Null(draft.AssignResponsibility(Scope(draft, 1), Uuid.CreateVersion4(),
            assignedApprover, ResponsibilityType.PolicyApprover, AuthorId, "Author", Now, Now,
            null, []));
        Assert.Null(Review(draft, 1, reviewId, ReviewerId));

        // Act
        var unassigned = Approve(draft, 1, reviewId, ApproverId);
        var assigned = Approve(draft, 1, reviewId, assignedApprover);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(unassigned).Code);
        Assert.Null(assigned);
        Assert.Equal(1, draft.EffectiveVersionCount);
    }

    [Fact]
    public void ShouldRejectResponsibilityGivenStaleRevisionScope()
    {
        // Arrange
        var draft = Draft();

        // Act
        var failure = draft.AssignResponsibility(Scope(draft, 2), Uuid.CreateVersion4(),
            ReviewerId, ResponsibilityType.AssignedReviewer, AuthorId, "Author", Now, Now,
            null, []);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(failure).Code);
    }

    static SeparationOfDutiesWaiver CreateWaiver(SeparationOfDutiesWaiverScope scope,
        Uuid beneficiary)
    {
        var waiver = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(scope, beneficiary, Uuid.CreateVersion4(), "Requester",
            "No other qualified approver is available.", Now, Now.AddDays(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Approver", Now.AddMinutes(1)));
        return waiver;
    }
}
