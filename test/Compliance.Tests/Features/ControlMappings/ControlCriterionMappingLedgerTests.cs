using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.ControlMappings;

public sealed class ControlCriterionMappingLedgerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid ControlVersionId = Uuid.CreateVersion4();
    static readonly Uuid EditionId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldAllowProposerReviewGivenApprovedWaiverForExactRevision()
    {
        // Arrange
        var ledger = new ControlCriterionMappingLedger(TenantId, ProgramId);
        var mappingId = Propose(ledger);
        var waiver = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(new SeparationOfDutiesWaiverScope(
                SeparationOfDutiesRecordTypes.ControlCriterionMapping, mappingId, mappingId, 1,
                SeparationOfDutiesActions.Review), AuthorId, Uuid.CreateVersion4(), "Requester",
            "No other qualified reviewer is available.", Now, Now.AddDays(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Approver", Now.AddMinutes(1)));
        var wrongRevision = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        Assert.Null(wrongRevision.Record(new SeparationOfDutiesWaiverScope(
                SeparationOfDutiesRecordTypes.ControlCriterionMapping, mappingId, mappingId, 7,
                SeparationOfDutiesActions.Review), AuthorId, Uuid.CreateVersion4(), "Requester",
            "No other qualified reviewer is available.", Now, Now.AddDays(1)));
        Assert.Null(wrongRevision.Approve(Uuid.CreateVersion4(), "Approver", Now.AddMinutes(1)));

        // Act
        var denied = ledger.Review(mappingId, 1, Uuid.CreateVersion4(), "accept", "Fits",
            AuthorId, "Author", Now.AddMinutes(2), wrongRevision);
        var waived = ledger.Review(mappingId, 1, Uuid.CreateVersion4(), "accept", "Fits",
            AuthorId, "Author", Now.AddMinutes(2), waiver);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited, Assert.IsType<CommandFailure>(denied).Code);
        Assert.Null(waived);
        var view = Assert.IsType<ControlCriterionMappingView>(ledger.Read(mappingId));
        Assert.Equal("active", view.Status);
        Assert.Equal(waiver.Id, view.Versions[0].SeparationOfDutiesWaiverId);
    }

    [Fact]
    public void ShouldRejectInvalidReviewGivenUnknownOutcomeOrStaleRevision()
    {
        // Arrange
        var ledger = new ControlCriterionMappingLedger(TenantId, ProgramId);
        var mappingId = Propose(ledger);

        // Act
        var outcome = ledger.Review(mappingId, 1, Uuid.CreateVersion4(), "satisfied", "Fits",
            ReviewerId, "Reviewer", Now);
        var stale = ledger.Review(mappingId, 0, Uuid.CreateVersion4(), "accept", "Fits",
            ReviewerId, "Reviewer", Now);
        var missing = ledger.Review(Uuid.CreateVersion4(), 1, Uuid.CreateVersion4(), "accept",
            "Fits", ReviewerId, "Reviewer", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(outcome).Code);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(missing).Code);
    }

    [Fact]
    public void ShouldReplaySameMappingsGivenRoundTrippedEvents()
    {
        // Arrange
        var source = new ControlCriterionMappingLedger(TenantId, ProgramId);
        var mappingId = Propose(source);
        Assert.Null(source.Review(mappingId, 1, Uuid.CreateVersion4(), "accept", "Fits",
            ReviewerId, "Reviewer", Now));
        Assert.Null(source.Retire(mappingId, 2, "Superseded by redesign", ReviewerId, "Reviewer",
            Now.AddDays(1)));
        var events = new AggregateScenario<ControlCriterionMappingLedger>(source).PendingEvents;
        var seeded = events.Select((ev, index) =>
            DomainEventSeed.Attach(RoundTrip(ev), source.Id, (ulong)index + 1)).ToArray();

        // Act
        var replayed = new AggregateScenario<ControlCriterionMappingLedger>(
            new ControlCriterionMappingLedger(TenantId, ProgramId)).Given(seeded).Aggregate;

        // Assert
        var expected = Assert.IsType<ControlCriterionMappingView>(source.Read(mappingId));
        var actual = Assert.IsType<ControlCriterionMappingView>(replayed.Read(mappingId));
        Assert.Equal(expected with { Versions = [] }, actual with { Versions = [] });
        Assert.Equal(expected.Versions, actual.Versions);
        Assert.Equal("retired", actual.Status);
    }

    static Uuid Propose(ControlCriterionMappingLedger ledger)
    {
        Assert.Null(ledger.Propose(ControlId, ControlVersionId, EditionId, "CC6.1", "criterion",
            0, "Restricts access", "All production systems", AuthorId, "Author", Now,
            out var registration));
        return Assert.IsType<ControlCriterionMappingRegistration>(registration).MappingId;
    }

    static DomainEvent RoundTrip(DomainEvent ev) => (DomainEvent)JsonSerializer.Deserialize(
        JsonSerializer.Serialize(ev, ev.GetType(), ComplianceCoreJsonContext.Default),
        ev.GetType(), ComplianceCoreJsonContext.Default)!;
}
