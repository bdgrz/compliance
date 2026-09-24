using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskDraftActorSnapshotTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly DateTimeOffset At = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
    static readonly string ContentJson = """{"title":"Provider outage","scenario":"Provider unavailable","potential_effect":"Requests cannot be processed","source_note":"Management note"}""";

    [Fact]
    public void ShouldSnapshotEachActorDisplayGivenDisplayChangesBetweenEvents()
    {
        // Arrange
        var riskId = RiskDraft.IdFor(TenantId, ProgramId, "R-01");
        var risk = new RiskDraft(TenantId, riskId);
        Assert.True(risk.Create(ProgramId, Uuid.CreateVersion4(), "R-01", Content(),
            MemberId, "First display", At).IsSuccess);
        Assert.True(risk.Revise(ProgramId, 1, Content() with { Scenario = "Extended outage" },
            MemberId, "Second display", At.AddMinutes(1)).IsSuccess);

        // Act
        var events = new AggregateScenario<RiskDraft>(risk).PendingEvents;

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "First display"),
            Assert.IsType<RiskDraftCreated>(events[0]).StoredActor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Second display"),
            Assert.IsType<RiskDraftRevised>(events[1]).StoredActor);
    }

    [Fact]
    public void ShouldReplayLegacyEventsWithMemberActorGivenEventsWithoutActor()
    {
        // Arrange
        var riskId = RiskDraft.IdFor(TenantId, ProgramId, "R-01");
        var created = Deserialize<RiskDraftCreated>($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","risk_id":"{{riskId}}","create_request_id":"{{Uuid.CreateVersion4()}}","identifier":"R-01","content":{{ContentJson}},"actor_member_id":"{{MemberId}}","actor_display":"First display","changed_at":"2026-09-23T12:00:00+00:00"}
            """);
        var revised = Deserialize<RiskDraftRevised>($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","risk_id":"{{riskId}}","revision":2,"content":{{ContentJson}},"actor_member_id":"{{MemberId}}","actor_display":"Second display","changed_at":"2026-09-23T12:01:00+00:00"}
            """);

        // Act
        var replayed = new AggregateScenario<RiskDraft>(new RiskDraft(TenantId, riskId))
            .Given(DomainEventSeed.Attach(created, riskId, 1),
                DomainEventSeed.Attach(revised, riskId, 2)).Aggregate;

        // Assert
        Assert.True(replayed.IsCreated);
        Assert.Equal(2, replayed.Revision);
        Assert.Null(created.StoredActor);
        Assert.Null(revised.StoredActor);
        Assert.Equal(ActorReference.ForMember(MemberId, "First display"), created.Actor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Second display"), revised.Actor);
    }

    [Fact]
    public void ShouldSerializeMemberLastChangedByGivenPrePrDraftViewJson()
    {
        // Arrange
        var json = $$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","risk_id":"{{Uuid.CreateVersion4()}}","identifier":"R-01","revision":2,"status":"draft","owner_resolution":"unresolved","content":{{ContentJson}},"last_changed_by_member_id":"{{MemberId}}","last_changed_by_display":"Second display","last_changed_at":"2026-09-23T12:01:00+00:00"}
            """;

        // Act
        var output = JsonNode.Parse(JsonSerializer.Serialize(Deserialize<RiskDraftView>(json),
            ComplianceCoreJsonContext.Default.RiskDraftView))!;

        // Assert
        Assert.Equal("member", (string?)output["last_changed_by"]?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)output["last_changed_by"]?["id"]);
        Assert.Equal("Second display", (string?)output["last_changed_by"]?["display"]);
    }

    [Fact]
    public void ShouldSerializeMemberActorGivenPrePrRevisionViewJson()
    {
        // Arrange
        var json = $$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","risk_id":"{{Uuid.CreateVersion4()}}","identifier":"R-01","revision":1,"content":{{ContentJson}},"changed_by_member_id":"{{MemberId}}","changed_by_display":"First display","changed_at":"2026-09-23T12:00:00+00:00"}
            """;

        // Act
        var output = JsonNode.Parse(JsonSerializer.Serialize(
            Deserialize<RiskDraftRevisionView>(json),
            ComplianceCoreJsonContext.Default.RiskDraftRevisionView))!;

        // Assert
        Assert.Equal("member", (string?)output["actor"]?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)output["actor"]?["id"]);
        Assert.Equal("First display", (string?)output["actor"]?["display"]);
    }

    static T Deserialize<T>(string json) where T : class =>
        Assert.IsType<T>(JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!));

    static RiskDraftContent Content() => new("Provider outage", "Provider unavailable",
        "Requests cannot be processed", "Management note");
}
