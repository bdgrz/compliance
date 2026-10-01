using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.ControlMappings;

public sealed class CriterionApplicabilityLedgerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid EditionId = Uuid.CreateVersion4();
    static readonly Uuid ProposerId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldExcludeCriterionGivenAcceptedReviewThenRestoreGivenWithdrawal()
    {
        // Arrange
        var ledger = New();
        var registration = Propose(ledger, 0);

        // Act
        var review = ledger.Review(registration.DecisionId, 1, Uuid.CreateVersion4(), "accept",
            "Supported.", ReviewerId, Actor(ReviewerId), Now.AddMinutes(1));
        var accepted = ledger.Read(registration.DecisionId)!;
        var withdrawal = ledger.Withdraw(registration.DecisionId, 2, "Now applies.",
            Actor(ProposerId), Now.AddMinutes(2));

        // Assert
        Assert.Null(review);
        Assert.Equal("not_applicable", accepted.Status);
        Assert.Equal(1, accepted.ActiveVersionNumber);
        Assert.Null(withdrawal);
        var withdrawn = ledger.Read(registration.DecisionId)!;
        Assert.Equal("withdrawn", withdrawn.Status);
        Assert.Null(withdrawn.ActiveVersionNumber);
        Assert.Equal(Actor(ProposerId), withdrawn.Versions[0].WithdrawnBy);
        Assert.Equal(CriterionApplicabilityLedger.DecisionIdFor(ProgramId, EditionId, "CC6.2"),
            registration.DecisionId);
    }

    [Fact]
    public void ShouldRejectSelfReviewGivenProposerWithoutExactWaiver()
    {
        // Arrange
        var ledger = New();
        var registration = Propose(ledger, 0);
        var waiver = Waiver(new SeparationOfDutiesWaiverScope("criterion_applicability",
            registration.DecisionId, registration.DecisionId, 1, "review"));

        // Act
        var self = ledger.Review(registration.DecisionId, 1, Uuid.CreateVersion4(), "accept",
            "Mine.", ProposerId, Actor(ProposerId), Now.AddHours(1));
        var waived = ledger.Review(registration.DecisionId, 1, Uuid.CreateVersion4(), "accept",
            "Waived.", ProposerId, Actor(ProposerId), Now.AddHours(1), waiver);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, self!.Code);
        Assert.Null(waived);
        Assert.Equal(waiver.Id, ledger.Read(registration.DecisionId)!.Versions[0]
            .SeparationOfDutiesWaiverId);
    }

    [Fact]
    public void ShouldRejectProposalGivenPendingStaleOrAlreadyNotApplicable()
    {
        // Arrange
        var ledger = New();
        var first = Propose(ledger, 0);

        // Act
        var retry = ledger.Propose(EditionId, "CC6.2", 0, "No accounts are approved.",
            ProposerId, Actor(ProposerId), Now, out var replayed);
        var pending = ledger.Propose(EditionId, "CC6.2", 1, "Other.", ProposerId,
            Actor(ProposerId), Now, out _);
        Assert.Null(ledger.Review(first.DecisionId, 1, Uuid.CreateVersion4(), "accept", "Ok.",
            ReviewerId, Actor(ReviewerId), Now));
        var stale = ledger.Propose(EditionId, "CC6.2", 1, "Stale.", ProposerId,
            Actor(ProposerId), Now, out _);
        var duplicate = ledger.Propose(EditionId, "CC6.2", 2, "Again.", ProposerId,
            Actor(ProposerId), Now, out _);

        // Assert
        Assert.Null(retry);
        Assert.Equal(first, replayed);
        Assert.Equal(CommandFailureCode.StateConflict, pending!.Code);
        Assert.Equal(CommandFailureCode.VersionConflict, stale!.Code);
        Assert.Equal(CommandFailureCode.StateConflict, duplicate!.Code);
        Assert.Equal(2, new AggregateScenario<CriterionApplicabilityLedger>(ledger)
            .PendingEvents.Count);
    }

    [Fact]
    public void ShouldMatchProjectedViewsGivenReducerFoldsLedgerEvents()
    {
        // Arrange
        var ledger = New();
        var registration = Propose(ledger, 0);
        Assert.Null(ledger.Review(registration.DecisionId, 1, Uuid.CreateVersion4(), "reject",
            "Unsupported.", ReviewerId, Actor(ReviewerId), Now));
        Assert.Null(ledger.Propose(EditionId, "CC6.2", 2, "Second attempt.", ProposerId,
            Actor(ProposerId), Now, out _));
        Assert.Null(ledger.Review(registration.DecisionId, 3, Uuid.CreateVersion4(), "accept",
            "Supported.", ReviewerId, Actor(ReviewerId), Now));
        var events = new AggregateScenario<CriterionApplicabilityLedger>(ledger).PendingEvents;

        // Act
        CriterionApplicabilityView? projected = null;
        foreach (var domainEvent in events)
            projected = CriterionApplicabilityReducer.Apply(projected, domainEvent);

        // Assert
        Assert.Equal(ledger.Read(registration.DecisionId)!.Status, projected!.Status);
        Assert.Equal(ledger.Read(registration.DecisionId)!.Versions, projected.Versions);
        Assert.Equal(["rejected", "accepted"], projected.Versions.Select(v => v.Status));
    }

    static CriterionApplicabilityLedger New() => new(TenantId, ProgramId);

    static ActorReference Actor(Uuid memberId) => ActorReference.ForMember(memberId, "Member");

    static CriterionApplicabilityRegistration Propose(CriterionApplicabilityLedger ledger,
        long revision)
    {
        Assert.Null(ledger.Propose(EditionId, "CC6.2", revision, "No accounts are approved.",
            ProposerId, Actor(ProposerId), Now, out var registration));
        return registration!;
    }

    static SeparationOfDutiesWaiver Waiver(SeparationOfDutiesWaiverScope scope)
    {
        var waiver = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(scope, ProposerId, Uuid.CreateVersion4(), "Requester",
            "Sole qualified reviewer.", Now, Now.AddDays(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Approver", Now.AddMinutes(1)));
        return waiver;
    }
}
