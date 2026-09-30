using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlActivationTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly EffectiveFrom = new(2026, 10, 1);

    [Fact]
    public void ShouldActivateImmutableVersionGivenIndependentReviewAndVerifiedOwner()
    {
        // Arrange
        var control = Created("AC-01", out var scope);
        var ownerAssignment = Uuid.CreateVersion4();
        Assert.Null(AssignOwner(control, scope, ownerAssignment, OwnerId));
        var reviewId = Uuid.CreateVersion4();
        var approvalId = Uuid.CreateVersion4();
        Assert.Null(control.Review(ProgramId, 1, reviewId, "accept", "Evidence is defined.",
            ReviewerId, "Reviewer", Now.AddMinutes(2)));

        // Act
        var approved = control.Approve(ProgramId, 1, approvalId, reviewId, EffectiveFrom,
            "Ready to operate.", new HashSet<Uuid> { OwnerId }, ApproverId, "Approver",
            Now.AddMinutes(3));
        var revise = control.Revise(ProgramId, 1, Content() with { Title = "Changed" },
            AuthorId, "Author", Now.AddMinutes(4));
        var discard = control.Discard(ProgramId, 1, "No longer needed", AuthorId, "Author",
            Now.AddMinutes(4));

        // Assert
        Assert.Null(approved);
        var version = Assert.IsType<ControlVersionView>(control.ApprovedVersion);
        Assert.Equal(ControlVersionIds.Initial(control.Id), version.VersionId);
        Assert.Equal(1, version.Revision);
        Assert.Equal("approved", version.Status);
        Assert.Equal("organization_authored", version.ContentOrigin);
        Assert.Equal("verified_member", version.OwnerResolution);
        Assert.Equal(OwnerId, version.OwnerMemberId);
        Assert.Equal(ownerAssignment, version.OwnerAssignmentId);
        Assert.Equal(reviewId, version.AcceptedReviewDecisionId);
        Assert.Equal(approvalId, version.ApprovalDecisionId);
        Assert.Equal(EffectiveFrom, version.EffectiveFrom);
        Assert.Null(version.PredecessorVersionId);
        Assert.Equal(Content().Title, version.Content.Title);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(revise).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(discard).Code);
        Assert.Equal(1, control.Revision);
        Assert.Equal(["review", "approval"], control.ReadDecisions().Select(d => d.Kind));
    }

    [Fact]
    public void ShouldRejectApprovalGivenOwnerWithoutCurrentActiveMembership()
    {
        // Arrange
        var control = Created("AC-02", out var scope);
        Assert.Null(AssignOwner(control, scope, Uuid.CreateVersion4(), OwnerId));
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(control.Review(ProgramId, 1, reviewId, "accept", "Reviewed",
            ReviewerId, "Reviewer", Now.AddMinutes(2)));

        // Act
        var suspended = control.Approve(ProgramId, 1, Uuid.CreateVersion4(), reviewId,
            EffectiveFrom, "Approved", new HashSet<Uuid>(), ApproverId, "Approver",
            Now.AddMinutes(3));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(suspended).Code);
        Assert.Null(control.ApprovedVersion);
    }

    [Fact]
    public void ShouldRejectApprovalGivenOnlyUnverifiedOwnerReference()
    {
        // Arrange
        var control = Created("AC-03", out _);
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(control.Review(ProgramId, 1, reviewId, "accept", "Reviewed",
            ReviewerId, "Reviewer", Now.AddMinutes(2)));

        // Act
        var result = control.Approve(ProgramId, 1, Uuid.CreateVersion4(), reviewId,
            EffectiveFrom, "Approved", new HashSet<Uuid> { OwnerId }, ApproverId, "Approver",
            Now.AddMinutes(3));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(result).Code);
    }

    [Fact]
    public void ShouldRequireLatestAcceptedReviewGivenRevisionAfterReview()
    {
        // Arrange
        var control = Created("AC-04", out var scope);
        Assert.Null(AssignOwner(control, scope, Uuid.CreateVersion4(), OwnerId));
        var firstReview = Uuid.CreateVersion4();
        Assert.Null(control.Review(ProgramId, 1, firstReview, "accept", "Reviewed",
            ReviewerId, "Reviewer", Now.AddMinutes(2)));
        Assert.Null(control.Revise(ProgramId, 1, Content() with { Title = "Revised" },
            AuthorId, "Author", Now.AddMinutes(3)));

        // Act
        var stale = control.Approve(ProgramId, 1, Uuid.CreateVersion4(), firstReview,
            EffectiveFrom, "Approved", new HashSet<Uuid> { OwnerId }, ApproverId, "Approver",
            Now.AddMinutes(4));
        var unreviewed = control.Approve(ProgramId, 2, Uuid.CreateVersion4(), firstReview,
            EffectiveFrom, "Approved", new HashSet<Uuid> { OwnerId }, ApproverId, "Approver",
            Now.AddMinutes(4));

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(unreviewed).Code);
    }

    [Fact]
    public void ShouldRecordSupersedingDecisionGivenRepeatedReview()
    {
        // Arrange
        var control = Created("AC-05", out _);
        var changes = Uuid.CreateVersion4();
        var accepted = Uuid.CreateVersion4();
        Assert.Null(control.Review(ProgramId, 1, changes, "request_changes", "Clarify evidence.",
            ReviewerId, "Reviewer", Now.AddMinutes(2)));

        // Act
        var result = control.Review(ProgramId, 1, accepted, "accept", "Clarified.",
            ReviewerId, "Reviewer", Now.AddMinutes(3));

        // Assert
        Assert.Null(result);
        var decisions = control.ReadDecisions();
        Assert.Equal(2, decisions.Count);
        Assert.Null(decisions[0].SupersedesDecisionId);
        Assert.Equal(changes, decisions[1].SupersedesDecisionId);
        Assert.Equal(ActorReference.ForMember(ReviewerId, "Reviewer"), decisions[1].Actor);
    }

    [Fact]
    public void ShouldDenySelfReviewGivenAuthorWithoutExactWaiver()
    {
        // Arrange
        var control = Created("AC-06", out var scope);
        var waiver = CreateWaiver(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Control, control.Id, scope.VersionId, 1,
            SeparationOfDutiesActions.Review), AuthorId);
        var otherRevision = CreateWaiver(new SeparationOfDutiesWaiverScope(
            SeparationOfDutiesRecordTypes.Control, control.Id, scope.VersionId, 2,
            SeparationOfDutiesActions.Review), AuthorId);

        // Act
        var denied = control.Review(ProgramId, 1, Uuid.CreateVersion4(), "accept", "Mine",
            AuthorId, "Author", Now.AddMinutes(2));
        var deniedMismatch = control.Review(ProgramId, 1, Uuid.CreateVersion4(), "accept",
            "Mine", AuthorId, "Author", Now.AddMinutes(2), otherRevision);
        var waived = control.Review(ProgramId, 1, Uuid.CreateVersion4(), "accept", "Waived",
            AuthorId, "Author", Now.AddMinutes(2), waiver);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(denied).Code);
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(deniedMismatch).Code);
        Assert.Null(waived);
        Assert.Equal(waiver.Id, Assert.Single(control.ReadDecisions()).SeparationOfDutiesWaiverId);
    }

    [Fact]
    public void ShouldDenyOwnerSelfApprovalGivenNoWaiver()
    {
        // Arrange
        var control = Created("AC-07", out var scope);
        Assert.Null(AssignOwner(control, scope, Uuid.CreateVersion4(), OwnerId));
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(control.Review(ProgramId, 1, reviewId, "accept", "Reviewed",
            ReviewerId, "Reviewer", Now.AddMinutes(2)));

        // Act
        var result = control.Approve(ProgramId, 1, Uuid.CreateVersion4(), reviewId,
            EffectiveFrom, "Approving my own control.", new HashSet<Uuid> { OwnerId }, OwnerId,
            "Owner", Now.AddMinutes(3));

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(result).Code);
    }

    [Fact]
    public void ShouldRejectResponsibilityGivenStaleOrForeignControlScope()
    {
        // Arrange
        var control = Created("AC-08", out var scope);

        // Act
        var stale = AssignOwner(control, scope with { Revision = 2 }, Uuid.CreateVersion4(),
            OwnerId);
        var wrongVersion = AssignOwner(control, scope with { VersionId = Uuid.CreateVersion4() },
            Uuid.CreateVersion4(), OwnerId);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(wrongVersion).Code);
    }

    [Fact]
    public void ShouldRejectReviewedDraftDiscardGivenRetainedDecisionHistory()
    {
        // Arrange
        var control = Created("AC-09", out _);
        Assert.Null(control.Review(ProgramId, 1, Uuid.CreateVersion4(), "request_changes",
            "Needs work", ReviewerId, "Reviewer", Now.AddMinutes(2)));

        // Act
        var result = control.Discard(ProgramId, 1, "Abandon", AuthorId, "Author",
            Now.AddMinutes(3));

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(result).Code);
    }

    [Fact]
    public void ShouldReplayActivationGivenPersistedEvents()
    {
        // Arrange
        var source = Created("AC-10", out var scope);
        Assert.Null(AssignOwner(source, scope, Uuid.CreateVersion4(), OwnerId));
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(source.Review(ProgramId, 1, reviewId, "accept", "Reviewed",
            ReviewerId, "Reviewer", Now.AddMinutes(2)));
        Assert.Null(source.Approve(ProgramId, 1, Uuid.CreateVersion4(), reviewId, EffectiveFrom,
            "Approved", new HashSet<Uuid> { OwnerId }, ApproverId, "Approver", Now.AddMinutes(3)));
        var events = new AggregateScenario<ControlDraft>(source).PendingEvents;
        var seeded = events.Select((ev, index) =>
            DomainEventSeed.Attach(RoundTrip(ev), source.Id, (ulong)index + 1)).ToArray();

        // Act
        var replayed = new AggregateScenario<ControlDraft>(new ControlDraft(TenantId, source.Id))
            .Given(seeded).Aggregate;

        // Assert
        var expected = Assert.IsType<ControlVersionView>(source.ApprovedVersion);
        var actual = Assert.IsType<ControlVersionView>(replayed.ApprovedVersion);
        Assert.Equal(expected, actual with { Content = expected.Content });
        Assert.Equal(expected.Content.ExpectedEvidenceDescriptions,
            actual.Content.ExpectedEvidenceDescriptions);
        Assert.Equal(source.ReadDecisions(), replayed.ReadDecisions());
        Assert.Equal(1, replayed.Revision);
    }

    static DomainEvent RoundTrip(DomainEvent ev) => (DomainEvent)JsonSerializer.Deserialize(
        JsonSerializer.Serialize(ev, ev.GetType(), ComplianceCoreJsonContext.Default),
        ev.GetType(), ComplianceCoreJsonContext.Default)!;

    static ControlDraft Created(string identifier, out ResponsibilityScope scope)
    {
        var controlId = ControlDraft.IdFor(TenantId, ProgramId, identifier);
        var control = new ControlDraft(TenantId, controlId);
        Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), identifier, Content(),
            AuthorId, "Author", Now).IsSuccess);
        scope = new ResponsibilityScope("control", controlId, ControlVersionIds.Initial(controlId), 1);
        return control;
    }

    static CommandFailure? AssignOwner(ControlDraft control, ResponsibilityScope scope,
        Uuid assignmentId, Uuid memberId) => control.AssignResponsibility(scope, assignmentId,
        memberId, ResponsibilityType.ControlOwner, AuthorId, "Author", Now.AddMinutes(1),
        Now, null, []);

    static SeparationOfDutiesWaiver CreateWaiver(SeparationOfDutiesWaiverScope scope,
        Uuid beneficiary)
    {
        var waiver = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(scope, beneficiary, Uuid.CreateVersion4(), "Requester",
            "No other qualified reviewer is available.", Now, Now.AddDays(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Approver", Now.AddMinutes(1)));
        return waiver;
    }

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews access", "The security lead reviews access quarterly.",
        ["Dated review record"], "Security lead");
}
