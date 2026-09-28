using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Boundaries;

public sealed class BoundaryActorSnapshotTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid BoundaryId = Uuid.CreateVersion4();
    static readonly Uuid DecisionId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();

    [Fact]
    public void ShouldRoundTripReviewActorAndReadLegacyEventGivenMissingSnapshot()
    {
        // Arrange
        var legacyEventJson = $$"""
            {"tenant_id":"{{TenantId}}","boundary_id":"{{BoundaryId}}","draft_version_id":"{{Uuid.CreateVersion4()}}","revision":1,"decision_id":"{{DecisionId}}","outcome":"accept","actor_member_id":"{{MemberId}}","actor_display":"Reviewer at decision time","rationale":"Reviewed","decided_at":"2026-09-27T12:00:00+00:00"}
            """;
        var legacyEvent = Assert.IsType<BoundaryReviewed>(JsonSerializer.Deserialize(
            legacyEventJson, ComplianceCoreJsonContext.Default.BoundaryReviewed));
        var capturedActor = ActorReference.ForMember(MemberId, "Reviewer at decision time");
        var currentEvent = legacyEvent with { StoredActor = capturedActor };

        // Act
        var eventJson = JsonNode.Parse(JsonSerializer.Serialize(currentEvent,
            ComplianceCoreJsonContext.Default.BoundaryReviewed))!;

        // Assert
        Assert.Null(legacyEvent.StoredActor);
        Assert.Equal(capturedActor, legacyEvent.Actor);
        Assert.Equal("member", (string?)eventJson["actor"]?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)eventJson["actor"]?["id"]);
        Assert.Equal("Reviewer at decision time", (string?)eventJson["actor"]?["display"]);
    }

    [Fact]
    public void ShouldRoundTripDiscardActorAndReadLegacyEventGivenMissingSnapshot()
    {
        // Arrange
        var legacyEventJson = $$"""
            {"tenant_id":"{{TenantId}}","boundary_id":"{{BoundaryId}}","draft_version_id":"{{Uuid.CreateVersion4()}}","revision":1,"actor_member_id":"{{MemberId}}","actor_display":"Discarder at discard time","rationale":"Withdrawn","discarded_at":"2026-09-27T12:00:00+00:00"}
            """;
        var legacyEvent = Assert.IsType<BoundaryDraftDiscarded>(JsonSerializer.Deserialize(
            legacyEventJson, ComplianceCoreJsonContext.Default.BoundaryDraftDiscarded));
        var capturedActor = ActorReference.ForMember(MemberId, "Discarder at discard time");
        var currentEvent = legacyEvent with { StoredActor = capturedActor };

        // Act
        var eventJson = JsonNode.Parse(JsonSerializer.Serialize(currentEvent,
            ComplianceCoreJsonContext.Default.BoundaryDraftDiscarded))!;

        // Assert
        Assert.Null(legacyEvent.StoredActor);
        Assert.Equal(capturedActor, legacyEvent.Actor);
        Assert.Equal("member", (string?)eventJson["actor"]?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)eventJson["actor"]?["id"]);
        Assert.Equal("Discarder at discard time", (string?)eventJson["actor"]?["display"]);
    }

    [Fact]
    public void ShouldSerializeDecisionActorAndReadLegacyRowsGivenMissingSnapshot()
    {
        // Arrange
        var legacyDecisionJson = $$"""
            {"tenant_id":"{{TenantId}}","boundary_id":"{{BoundaryId}}","decision_id":"{{DecisionId}}","version_id":"{{Uuid.CreateVersion4()}}","revision":1,"outcome":"accept","actor_member_id":"{{MemberId}}","actor_display":"Reviewer at decision time","rationale":"Reviewed","decided_at":"2026-09-27T12:00:00+00:00","supersedes_decision_id":null,"relies_on_decision_id":null,"impact_digest":null}
            """;
        var legacyDecision = Assert.IsType<BoundaryDecisionView>(JsonSerializer.Deserialize(
            legacyDecisionJson,
            ComplianceCoreJsonContext.Default.BoundaryDecisionView));
        var currentDecision = legacyDecision with
        {
            Actor = ActorReference.ForMember(MemberId, "Reviewer at decision time"),
        };

        // Act
        var legacyJson = JsonNode.Parse(JsonSerializer.Serialize(legacyDecision,
            ComplianceCoreJsonContext.Default.BoundaryDecisionView))!;
        var currentJson = JsonNode.Parse(JsonSerializer.Serialize(currentDecision,
            ComplianceCoreJsonContext.Default.BoundaryDecisionView))!;

        // Assert
        AssertActor(legacyJson["actor"]);
        AssertActor(currentJson["actor"]);
    }

    static void AssertActor(JsonNode? actor)
    {
        Assert.Equal("member", (string?)actor?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)actor?["id"]);
        Assert.Equal("Reviewer at decision time", (string?)actor?["display"]);
    }
}
