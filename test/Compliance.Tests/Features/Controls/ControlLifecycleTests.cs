using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlLifecycleTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly EffectiveFrom = new(2026, 10, 1);
    static readonly DateOnly SuccessorFrom = new(2027, 1, 1);
    const string Digest = "ABC123";

    [Fact]
    public void ShouldSupersedePredecessorGivenIndependentlyApprovedSuccessor()
    {
        // Arrange
        var control = Approved("LC-01");
        var predecessor = control.ApprovedVersion!.VersionId;
        Assert.Null(control.ProposeSuccessor(ProgramId, predecessor,
            Content() with { Title = "Access review v2" }, AuthorId, "Author", Now.AddHours(1)));
        var successorId = control.DraftVersionId;
        var reviewId = ReviewAndAssignOwner(control, Now.AddHours(2));

        // Act
        var result = control.Approve(ProgramId, control.Revision, Uuid.CreateVersion4(), reviewId,
            SuccessorFrom, "Successor ready.", new HashSet<Uuid> { OwnerId }, ApproverId,
            "Approver", Now.AddHours(3), impactDigest: Digest);

        // Assert
        Assert.Null(result);
        Assert.NotEqual(predecessor, successorId);
        Assert.Equal(ControlVersionIds.Sequence(control.Id, 2), successorId);
        var versions = control.ReadVersions();
        Assert.Equal(2, versions.Count);
        Assert.Equal("superseded", versions[0].Status);
        Assert.Equal(SuccessorFrom, versions[0].EffectiveUntil);
        Assert.Equal("approved", versions[1].Status);
        Assert.Equal(predecessor, versions[1].PredecessorVersionId);
        Assert.Null(versions[1].EffectiveUntil);
        Assert.Equal("Access review v2", control.ApprovedVersion!.Content.Title);
        Assert.Equal(predecessor, control.EffectiveVersion(SuccessorFrom.AddDays(-1))!.VersionId);
        Assert.Equal(successorId, control.EffectiveVersion(SuccessorFrom)!.VersionId);
        Assert.False(control.HasOpenDraft);
    }

    [Fact]
    public void ShouldRejectSuccessorProposalGivenStaleApprovedVersionOrOpenDraft()
    {
        // Arrange
        var control = Approved("LC-02");
        var current = control.ApprovedVersion!.VersionId;

        // Act
        var stale = control.ProposeSuccessor(ProgramId, Uuid.CreateVersion4(), Content(),
            AuthorId, "Author", Now.AddHours(1));
        var first = control.ProposeSuccessor(ProgramId, current, Content(), AuthorId, "Author",
            Now.AddHours(1));
        var duplicate = control.ProposeSuccessor(ProgramId, current, Content(), AuthorId,
            "Author", Now.AddHours(1));

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Null(first);
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(duplicate).Code);
    }

    [Fact]
    public void ShouldRejectSuccessorApprovalGivenInvalidEffectiveDateOrMissingDigest()
    {
        // Arrange
        var control = Approved("LC-03");
        Assert.Null(control.ProposeSuccessor(ProgramId, control.ApprovedVersion!.VersionId,
            Content(), AuthorId, "Author", Now.AddHours(1)));
        var reviewId = ReviewAndAssignOwner(control, Now.AddHours(2));

        // Act
        var early = control.Approve(ProgramId, control.Revision, Uuid.CreateVersion4(), reviewId,
            EffectiveFrom, "Approved", new HashSet<Uuid> { OwnerId }, ApproverId, "Approver",
            Now.AddHours(3), impactDigest: Digest);
        var undigested = control.Approve(ProgramId, control.Revision, Uuid.CreateVersion4(),
            reviewId, SuccessorFrom, "Approved", new HashSet<Uuid> { OwnerId }, ApproverId,
            "Approver", Now.AddHours(3));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(early).Code);
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(undigested).Code);
        Assert.Single(control.ReadVersions());
    }

    [Fact]
    public void ShouldRejectSuccessorApprovalGivenSuccessorAuthorWithoutWaiver()
    {
        // Arrange
        var control = Approved("LC-04");
        Assert.Null(control.ProposeSuccessor(ProgramId, control.ApprovedVersion!.VersionId,
            Content(), ApproverId, "Approver", Now.AddHours(1)));
        var reviewId = ReviewAndAssignOwner(control, Now.AddHours(2));

        // Act
        var result = control.Approve(ProgramId, control.Revision, Uuid.CreateVersion4(), reviewId,
            SuccessorFrom, "Approved", new HashSet<Uuid> { OwnerId }, ApproverId, "Approver",
            Now.AddHours(3), impactDigest: Digest);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(result).Code);
    }

    [Fact]
    public void ShouldRetireCurrentVersionGivenIndependentReviewAndRetainHistory()
    {
        // Arrange
        var control = Approved("LC-05");
        var version = control.ApprovedVersion!;
        var retireOn = new DateOnly(2027, 3, 1);
        Assert.Null(control.ProposeRetirement(ProgramId, version.VersionId, retireOn,
            "Replaced by an automated control.", AuthorId, "Author", Now.AddHours(1)));
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(control.Review(ProgramId, control.Revision, reviewId, "accept",
            "Retirement is justified.", ReviewerId, "Reviewer", Now.AddHours(2)));
        var decisionId = Uuid.CreateVersion4();

        // Act
        var result = control.Retire(ProgramId, control.Revision, decisionId, reviewId, Digest,
            "Retire at quarter end.", ApproverId, "Approver", Now.AddHours(3));
        var successor = control.ProposeSuccessor(ProgramId, version.VersionId, Content(),
            AuthorId, "Author", Now.AddHours(4));

        // Assert
        Assert.Null(result);
        Assert.True(control.IsRetired);
        var retired = Assert.Single(control.ReadVersions());
        Assert.Equal("retired", retired.Status);
        Assert.Equal(retireOn, retired.EffectiveUntil);
        Assert.Equal(version.VersionId, control.EffectiveVersion(retireOn.AddDays(-1))!.VersionId);
        Assert.Null(control.EffectiveVersion(retireOn));
        Assert.Equal(["review", "approval", "review", "retirement"],
            control.ReadDecisions().Select(static decision => decision.Kind));
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(successor).Code);
    }

    [Fact]
    public void ShouldRejectRetirementGivenProposerApprovesOrUnreviewedProposal()
    {
        // Arrange
        var control = Approved("LC-06");
        Assert.Null(control.ProposeRetirement(ProgramId, control.ApprovedVersion!.VersionId,
            new DateOnly(2027, 3, 1), "No longer operated.", ApproverId, "Approver",
            Now.AddHours(1)));

        // Act
        var unreviewed = control.Retire(ProgramId, control.Revision, Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Digest, "Retire.", ReviewerId, "Reviewer", Now.AddHours(2));
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(control.Review(ProgramId, control.Revision, reviewId, "accept",
            "Justified.", ReviewerId, "Reviewer", Now.AddHours(2)));
        var selfApproved = control.Retire(ProgramId, control.Revision, Uuid.CreateVersion4(),
            reviewId, Digest, "Retire.", ApproverId, "Approver", Now.AddHours(3));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(unreviewed).Code);
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(selfApproved).Code);
        Assert.False(control.IsRetired);
    }

    [Fact]
    public void ShouldRejectRetirementProposalGivenEffectiveDateNotAfterVersionStart()
    {
        // Arrange
        var control = Approved("LC-07");

        // Act
        var result = control.ProposeRetirement(ProgramId, control.ApprovedVersion!.VersionId,
            EffectiveFrom, "Retire now.", AuthorId, "Author", Now.AddHours(1));

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(result).Code);
        Assert.Null(control.PendingRetirementId);
    }

    [Fact]
    public void ShouldRejectDiscardGivenDraftWithAssignedResponsibility()
    {
        // Arrange
        var controlId = ControlDraft.IdFor(TenantId, ProgramId, "LC-08");
        var control = new ControlDraft(TenantId, controlId);
        Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), "LC-08", Content(),
            AuthorId, "Author", Now).IsSuccess);
        var assignmentId = Uuid.CreateVersion4();
        Assert.Null(AssignOwner(control, assignmentId, Now));
        Assert.Null(control.RevokeResponsibility(Scope(control), assignmentId, AuthorId, "Author",
            Now.AddMinutes(2), "Assigned in error"));

        // Act
        var result = control.Discard(ProgramId, 1, "Unused", AuthorId, "Author",
            Now.AddMinutes(3));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(result).Code);
        Assert.True(control.IsVisible);
    }

    [Fact]
    public void ShouldRejectDiscardGivenSuccessorDraftOfApprovedControl()
    {
        // Arrange
        var control = Approved("LC-09");
        Assert.Null(control.ProposeSuccessor(ProgramId, control.ApprovedVersion!.VersionId,
            Content(), AuthorId, "Author", Now.AddHours(1)));

        // Act
        var result = control.Discard(ProgramId, control.Revision, "Unused", AuthorId, "Author",
            Now.AddHours(2));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(result).Code);
    }

    [Fact]
    public void ShouldReplaySupersessionAndRetirementGivenRoundTrippedEvents()
    {
        // Arrange
        var source = Approved("LC-10");
        Assert.Null(source.ProposeSuccessor(ProgramId, source.ApprovedVersion!.VersionId,
            Content() with { Title = "v2" }, AuthorId, "Author", Now.AddHours(1)));
        var reviewId = ReviewAndAssignOwner(source, Now.AddHours(2));
        Assert.Null(source.Approve(ProgramId, source.Revision, Uuid.CreateVersion4(), reviewId,
            SuccessorFrom, "Approved", new HashSet<Uuid> { OwnerId }, ApproverId, "Approver",
            Now.AddHours(3), impactDigest: Digest));
        Assert.Null(source.ProposeRetirement(ProgramId, source.ApprovedVersion!.VersionId,
            new DateOnly(2027, 6, 1), "Retire", AuthorId, "Author", Now.AddHours(4)));
        var retirementReview = Uuid.CreateVersion4();
        Assert.Null(source.Review(ProgramId, source.Revision, retirementReview, "accept", "Ok",
            ReviewerId, "Reviewer", Now.AddHours(5)));
        Assert.Null(source.Retire(ProgramId, source.Revision, Uuid.CreateVersion4(),
            retirementReview, Digest, "Retire", ApproverId, "Approver", Now.AddHours(6)));
        var events = new AggregateScenario<ControlDraft>(source).PendingEvents;
        var seeded = events.Select((ev, index) =>
            DomainEventSeed.Attach(RoundTrip(ev), source.Id, (ulong)index + 1)).ToArray();

        // Act
        var replayed = new AggregateScenario<ControlDraft>(new ControlDraft(TenantId, source.Id))
            .Given(seeded).Aggregate;

        // Assert
        Assert.Equal(source.ReadVersions().Select(Key), replayed.ReadVersions().Select(Key));
        Assert.Equal(source.ReadDecisions(), replayed.ReadDecisions());
        Assert.True(replayed.IsRetired);
        Assert.Equal(2, replayed.Revision);
    }

    static (Uuid, string, DateOnly, DateOnly?, Uuid?) Key(ControlVersionView version) =>
        (version.VersionId, version.Status, version.EffectiveFrom, version.EffectiveUntil,
            version.PredecessorVersionId);

    static DomainEvent RoundTrip(DomainEvent ev) => (DomainEvent)JsonSerializer.Deserialize(
        JsonSerializer.Serialize(ev, ev.GetType(), ComplianceCoreJsonContext.Default),
        ev.GetType(), ComplianceCoreJsonContext.Default)!;

    static ControlDraft Approved(string identifier)
    {
        var controlId = ControlDraft.IdFor(TenantId, ProgramId, identifier);
        var control = new ControlDraft(TenantId, controlId);
        Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), identifier, Content(),
            AuthorId, "Author", Now).IsSuccess);
        var reviewId = ReviewAndAssignOwner(control, Now);
        Assert.Null(control.Approve(ProgramId, 1, Uuid.CreateVersion4(), reviewId, EffectiveFrom,
            "Ready.", new HashSet<Uuid> { OwnerId }, ApproverId, "Approver", Now.AddMinutes(3)));
        return control;
    }

    static Uuid ReviewAndAssignOwner(ControlDraft control, DateTimeOffset at)
    {
        Assert.Null(AssignOwner(control, Uuid.CreateVersion4(), at));
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(control.Review(ProgramId, control.Revision, reviewId, "accept", "Reviewed.",
            ReviewerId, "Reviewer", at.AddMinutes(2)));
        return reviewId;
    }

    static ResponsibilityScope Scope(ControlDraft control) => new("control", control.Id,
        control.DraftVersionId, control.Revision);

    static CommandFailure? AssignOwner(ControlDraft control, Uuid assignmentId,
        DateTimeOffset at) => control.AssignResponsibility(Scope(control), assignmentId, OwnerId,
        ResponsibilityType.ControlOwner, AuthorId, "Author", at.AddMinutes(1), at.AddMinutes(-1),
        null, []);

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews access", "The security lead reviews access quarterly.",
        ["Dated review record"], "Security lead");
}
