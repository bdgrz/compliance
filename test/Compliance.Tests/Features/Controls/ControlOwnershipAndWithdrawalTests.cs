using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlOwnershipAndWithdrawalTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly Uuid ApproverId = Uuid.CreateVersion4();
    static readonly Uuid PersonId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly EffectiveFrom = new(2026, 10, 1);
    static readonly ActorReference Author = ActorReference.ForMember(AuthorId, "Author");

    [Fact]
    public void ShouldActivateGivenVerifiedPersonOwnerWhoDoesNotSignIn()
    {
        // Arrange
        var control = Draft("PO-01");
        Assert.Null(control.DesignatePersonOwner(ProgramId, 1, Uuid.CreateVersion4(), PersonId,
            null, "Operations manager owns the review.", AuthorId, Author, Now));
        var reviewId = Review(control);

        // Act
        var approved = control.Approve(ProgramId, 1, Uuid.CreateVersion4(), reviewId,
            EffectiveFrom, "Ready.", new HashSet<Uuid>(), ApproverId, "Approver",
            Now.AddMinutes(5), verifiedPersonOwner: new ControlPersonOwner(PersonId, null));

        // Assert
        Assert.Null(approved);
        var version = control.ApprovedVersion!;
        Assert.Equal("verified_person", version.OwnerResolution);
        Assert.Equal(PersonId, version.OwnerPersonId);
        Assert.Equal(Uuid.Empty, version.OwnerMemberId);
        Assert.Equal("organization_authored", version.ContentOrigin);
    }

    [Fact]
    public void ShouldRejectActivationGivenDesignatedPersonNotVerified()
    {
        // Arrange
        var control = Draft("PO-02");
        Assert.Null(control.DesignatePersonOwner(ProgramId, 1, Uuid.CreateVersion4(), PersonId,
            null, "Owner.", AuthorId, Author, Now));
        var reviewId = Review(control);

        // Act
        var unverified = control.Approve(ProgramId, 1, Uuid.CreateVersion4(), reviewId,
            EffectiveFrom, "Ready.", new HashSet<Uuid>(), ApproverId, "Approver",
            Now.AddMinutes(5));
        var otherPerson = control.Approve(ProgramId, 1, Uuid.CreateVersion4(), reviewId,
            EffectiveFrom, "Ready.", new HashSet<Uuid>(), ApproverId, "Approver",
            Now.AddMinutes(5), verifiedPersonOwner: new ControlPersonOwner(
                Uuid.CreateVersion4(), null));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, unverified!.Code);
        Assert.Equal(CommandFailureCode.StateConflict, otherPerson!.Code);
        Assert.False(control.IsApproved);
    }

    [Fact]
    public void ShouldDenyOwnerDecisionGivenCorrelatedPersonOwnerWithoutWaiver()
    {
        // Arrange
        var control = Draft("PO-03");
        Assert.Null(control.DesignatePersonOwner(ProgramId, 1, Uuid.CreateVersion4(), PersonId,
            ReviewerId, "Reviewer also owns the control.", AuthorId, Author, Now));

        // Act
        var review = control.Review(ProgramId, 1, Uuid.CreateVersion4(), "accept", "Mine",
            ReviewerId, "Reviewer", Now.AddMinutes(1));
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(control.Review(ProgramId, 1, reviewId, "accept", "Independent",
            OwnerId, "Other", Now.AddMinutes(2)));
        var approval = control.Approve(ProgramId, 1, Uuid.CreateVersion4(), reviewId,
            EffectiveFrom, "Ready.", new HashSet<Uuid>(), ApproverId, "Approver",
            Now.AddMinutes(5), verifiedPersonOwner: new ControlPersonOwner(PersonId, ApproverId));

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, review!.Code);
        Assert.Equal(CommandFailureCode.ActorProhibited, approval!.Code);
        Assert.False(control.IsApproved);
    }

    [Fact]
    public void ShouldRequireNewDesignationGivenRevisedDraft()
    {
        // Arrange
        var control = Draft("PO-04");
        Assert.Null(control.DesignatePersonOwner(ProgramId, 1, Uuid.CreateVersion4(), PersonId,
            null, "Owner.", AuthorId, Author, Now));

        // Act
        Assert.Null(control.Revise(ProgramId, 1, Content() with { Title = "Revised" },
            AuthorId, "Author", Now.AddMinutes(1)));
        var stale = control.DesignatePersonOwner(ProgramId, 1, Uuid.CreateVersion4(), PersonId,
            null, "Owner.", AuthorId, Author, Now.AddMinutes(2));

        // Assert
        Assert.Null(control.CurrentPersonOwner);
        Assert.Equal(CommandFailureCode.VersionConflict, stale!.Code);
    }

    [Fact]
    public void ShouldKeepApprovedVersionCurrentGivenWithdrawnSuccessor()
    {
        // Arrange
        var control = Approved("WD-01");
        var current = control.ApprovedVersion!;
        Assert.Null(control.ProposeSuccessor(ProgramId, current.VersionId,
            Content() with { Title = "Successor" }, AuthorId, "Author", Now.AddHours(1)));
        var proposedRevision = control.Revision;
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(control.Review(ProgramId, proposedRevision, reviewId, "accept", "Ok",
            ReviewerId, "Reviewer", Now.AddHours(2)));
        var decisionId = Uuid.CreateVersion4();

        // Act
        var withdrawn = control.Withdraw(ProgramId, proposedRevision, decisionId,
            "The change is no longer needed.", AuthorId, Author, Now.AddHours(3));
        var lateApproval = control.Approve(ProgramId, proposedRevision, Uuid.CreateVersion4(),
            reviewId, new DateOnly(2027, 1, 1), "Late", new HashSet<Uuid> { OwnerId },
            ApproverId, "Approver", Now.AddHours(4), impactDigest: "D");
        var again = control.ProposeSuccessor(ProgramId, current.VersionId,
            Content() with { Title = "Second attempt" }, AuthorId, "Author", Now.AddHours(5));

        // Assert
        Assert.Null(withdrawn);
        Assert.Equal(CommandFailureCode.StateConflict, lateApproval!.Code);
        Assert.Null(again);
        Assert.Equal(current.VersionId, Assert.Single(control.ReadVersions()).VersionId);
        var withdrawal = Assert.Single(control.ReadDecisions(),
            decision => decision.Kind == "withdrawal");
        Assert.Equal(decisionId, withdrawal.DecisionId);
        Assert.Equal("withdraw", withdrawal.Outcome);
        Assert.Equal(proposedRevision + 2, control.Revision);
        var pending = new AggregateScenario<ControlDraft>(control).PendingEvents;
        Assert.Contains(pending, ev => ev is ControlProposalWithdrawn { Kind: "successor" });
    }

    [Fact]
    public void ShouldWithdrawRetirementGivenPendingProposalOnly()
    {
        // Arrange
        var control = Approved("WD-02");
        var none = control.Withdraw(ProgramId, control.Revision, Uuid.CreateVersion4(),
            "Nothing pending.", AuthorId, Author, Now.AddHours(1));
        Assert.Null(control.ProposeRetirement(ProgramId, control.ApprovedVersion!.VersionId,
            new DateOnly(2027, 3, 1), "Retire.", AuthorId, "Author", Now.AddHours(1)));
        var revision = control.Revision;

        // Act
        var stale = control.Withdraw(ProgramId, revision + 1, Uuid.CreateVersion4(),
            "Keep the control.", AuthorId, Author, Now.AddHours(2));
        var withdrawn = control.Withdraw(ProgramId, revision, Uuid.CreateVersion4(),
            "Keep the control.", AuthorId, Author, Now.AddHours(2));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, none!.Code);
        Assert.Equal(CommandFailureCode.VersionConflict, stale!.Code);
        Assert.Null(withdrawn);
        Assert.Null(control.PendingRetirementId);
        Assert.Null(control.PendingTargetId);
        Assert.False(control.IsRetired);
        Assert.Null(control.ProposeRetirement(ProgramId, control.ApprovedVersion!.VersionId,
            new DateOnly(2027, 3, 1), "Retire again.", AuthorId, "Author", Now.AddHours(3)));
    }

    [Fact]
    public void ShouldReplayWithdrawalAndPersonOwnerGivenRoundTrippedEvents()
    {
        // Arrange
        var source = Draft("WD-03");
        Assert.Null(source.DesignatePersonOwner(ProgramId, 1, Uuid.CreateVersion4(), PersonId,
            null, "Owner.", AuthorId, Author, Now));
        var reviewId = Review(source);
        Assert.Null(source.Approve(ProgramId, 1, Uuid.CreateVersion4(), reviewId,
            EffectiveFrom, "Ready.", new HashSet<Uuid>(), ApproverId, "Approver",
            Now.AddMinutes(5), verifiedPersonOwner: new ControlPersonOwner(PersonId, null)));
        Assert.Null(source.ProposeSuccessor(ProgramId, source.ApprovedVersion!.VersionId,
            Content() with { Title = "v2" }, AuthorId, "Author", Now.AddHours(1)));
        Assert.Null(source.Withdraw(ProgramId, source.Revision, Uuid.CreateVersion4(),
            "Not needed.", AuthorId, Author, Now.AddHours(2)));
        var events = new AggregateScenario<ControlDraft>(source).PendingEvents;
        var seeded = events.Select((ev, index) =>
            DomainEventSeed.Attach(RoundTrip(ev), source.Id, (ulong)index + 1)).ToArray();

        // Act
        var replayed = new AggregateScenario<ControlDraft>(new ControlDraft(TenantId, source.Id))
            .Given(seeded).Aggregate;

        // Assert
        Assert.Equal(source.Revision, replayed.Revision);
        Assert.Equal(source.ReadDecisions(), replayed.ReadDecisions());
        Assert.Equal(PersonId, replayed.ApprovedVersion!.OwnerPersonId);
        Assert.False(replayed.HasOpenDraft);
        Assert.Equal("Access review", replayed.CurrentContent!.Title);
    }

    [Theory]
    [InlineData("template", "SOC 2 starter pack", true)]
    [InlineData("supplied", "Readiness advisor", true)]
    [InlineData("organization_authored", null, true)]
    [InlineData("template", null, false)]
    [InlineData("imported", "File", false)]
    public void ShouldRecordProvenanceGivenOrigin(string origin, string? source, bool valid)
    {
        // Arrange
        var control = new ControlDraft(TenantId, ControlDraft.IdFor(TenantId, ProgramId, "PV-01"));
        var content = Content() with
        {
            Provenance = new ControlContentProvenance(origin, source, " TPL-7 ", "2026.1"),
        };

        // Act
        var created = control.Create(ProgramId, Uuid.CreateVersion4(), "PV-01", content,
            AuthorId, "Author", Now);

        // Assert
        Assert.Equal(valid, created.IsSuccess);
        if (valid)
            Assert.Equal("TPL-7", control.CurrentContent!.Provenance!.SourceReference);
    }

    [Fact]
    public void ShouldCarryTemplateOriginGivenApprovedTemplateDraft()
    {
        // Arrange
        var control = new ControlDraft(TenantId, ControlDraft.IdFor(TenantId, ProgramId, "PV-02"));
        Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), "PV-02", Content() with
        {
            Provenance = new ControlContentProvenance("template", "Starter pack", "AC-1"),
        }, AuthorId, "Author", Now).IsSuccess);
        Assert.Null(AssignOwner(control));
        var reviewId = Review(control);

        // Act
        var approved = control.Approve(ProgramId, 1, Uuid.CreateVersion4(), reviewId,
            EffectiveFrom, "Ready.", new HashSet<Uuid> { OwnerId }, ApproverId, "Approver",
            Now.AddMinutes(5));

        // Assert
        Assert.Null(approved);
        Assert.Equal("template", control.ApprovedVersion!.ContentOrigin);
        Assert.Equal("verified_member", control.ApprovedVersion.OwnerResolution);
        Assert.Null(control.ApprovedVersion.OwnerPersonId);
    }

    static ControlDraft Draft(string identifier)
    {
        var control = new ControlDraft(TenantId, ControlDraft.IdFor(TenantId, ProgramId,
            identifier));
        Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), identifier, Content(),
            AuthorId, "Author", Now).IsSuccess);
        return control;
    }

    static ControlDraft Approved(string identifier)
    {
        var control = Draft(identifier);
        Assert.Null(AssignOwner(control));
        var reviewId = Review(control);
        Assert.Null(control.Approve(ProgramId, 1, Uuid.CreateVersion4(), reviewId, EffectiveFrom,
            "Ready.", new HashSet<Uuid> { OwnerId }, ApproverId, "Approver", Now.AddMinutes(5)));
        return control;
    }

    static Uuid Review(ControlDraft control)
    {
        var reviewId = Uuid.CreateVersion4();
        Assert.Null(control.Review(ProgramId, control.Revision, reviewId, "accept", "Reviewed.",
            ReviewerId, "Reviewer", Now.AddMinutes(2)));
        return reviewId;
    }

    static CommandFailure? AssignOwner(ControlDraft control) => control.AssignResponsibility(
        new ResponsibilityScope("control", control.Id, control.DraftVersionId, control.Revision),
        Uuid.CreateVersion4(), OwnerId, ResponsibilityType.ControlOwner, AuthorId, "Author",
        Now.AddMinutes(1), Now.AddMinutes(-1), null, []);

    static DomainEvent RoundTrip(DomainEvent ev) => (DomainEvent)JsonSerializer.Deserialize(
        JsonSerializer.Serialize(ev, ev.GetType(), ComplianceCoreJsonContext.Default),
        ev.GetType(), ComplianceCoreJsonContext.Default)!;

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews access", "The security lead reviews access quarterly.",
        ["Dated review record"]);
}
