using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.ControlMappings;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.ControlMappings;

public sealed class ControlCriterionMappingReducerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid EditionId = Uuid.CreateVersion4();
    static readonly Uuid ProposerId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldMatchLedgerReadsGivenProposeRejectRemapAcceptAndRetire()
    {
        // Arrange
        var ledger = new ControlCriterionMappingLedger(TenantId, ProgramId);
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        Assert.Null(Propose(ledger, first, 0));
        var mappingId = ledger.ReadAll()[0].MappingId;
        Assert.Null(Review(ledger, mappingId, 1, "reject"));
        Assert.Null(Propose(ledger, first, 2));
        Assert.Null(Review(ledger, mappingId, 3, "accept"));
        Assert.Null(Propose(ledger, second, 4));
        Assert.Null(Review(ledger, mappingId, 5, "accept"));
        Assert.Null(ledger.Retire(mappingId, 6, "Control retired.", ProposerId, "Proposer", Now));
        var events = new AggregateScenario<ControlCriterionMappingLedger>(ledger).PendingEvents
            .Select(RoundTrip).ToArray();

        // Act
        ControlCriterionMappingView? projected = null;
        var stages = new List<string>();
        foreach (var domainEvent in events)
        {
            projected = ControlCriterionMappingReducer.Apply(projected, domainEvent);
            stages.Add(projected.Status);
        }

        // Assert
        var expected = ledger.Read(mappingId)!;
        Assert.Equal(expected with { Versions = [] }, projected! with { Versions = [] });
        Assert.Equal(expected.Versions, projected.Versions);
        Assert.Equal(["pending", "rejected", "pending", "active", "pending", "active", "retired"],
            stages);
        Assert.Equal(["rejected", "superseded", "retired"],
            projected.Versions.Select(static version => version.Status));
    }

    static Bdgrz.Compliance.Features.Versioning.CommandFailure? Propose(
        ControlCriterionMappingLedger ledger, Uuid controlVersionId, long revision) =>
        ledger.Propose(ControlId, controlVersionId, EditionId, "CC6.1", "criterion", revision,
            "Rationale " + revision, "Applies to production.", ProposerId, "Proposer", Now,
            out _);

    static Bdgrz.Compliance.Features.Versioning.CommandFailure? Review(
        ControlCriterionMappingLedger ledger, Uuid mappingId, long revision, string outcome) =>
        ledger.Review(mappingId, revision, Uuid.CreateVersion4(), outcome, "Reviewed.",
            ReviewerId, "Reviewer", Now.AddMinutes(revision));

    static DomainEvent RoundTrip(DomainEvent ev) => (DomainEvent)JsonSerializer.Deserialize(
        JsonSerializer.Serialize(ev, ev.GetType(), ComplianceCoreJsonContext.Default),
        ev.GetType(), ComplianceCoreJsonContext.Default)!;
}
