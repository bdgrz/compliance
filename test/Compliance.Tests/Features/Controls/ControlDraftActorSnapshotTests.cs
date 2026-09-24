using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlDraftActorSnapshotTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly DateTimeOffset At = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
    static readonly string ContentJson = """{"title":"Access review","objective":"Review access","description":"Management reviews access","implementation_narrative":"The owner reviews access quarterly.","expected_evidence_descriptions":["Dated review record"]}""";

    [Fact]
    public void ShouldSnapshotEachActorDisplayGivenDisplayChangesBetweenEvents()
    {
        // Arrange
        var controlId = ControlDraft.IdFor(TenantId, ProgramId, "AC-01");
        var scenario = new AggregateScenario<ControlDraft>(new ControlDraft(TenantId, controlId));
        Assert.True(scenario.When(control => AggregateOutcome.CommitOnSuccess(control.Create(
            ProgramId, Uuid.CreateVersion4(), "AC-01", Content(), MemberId, "First display",
            At))).IsSuccess);
        var created = Assert.IsType<ControlDraftCreated>(Assert.Single(scenario.PendingEvents));
        Assert.True(scenario.When(control => AggregateOutcome.CommitOnSuccess(control.Revise(
            ProgramId, 1, Content() with { Title = "Updated review" }, MemberId, "Second display",
            At.AddMinutes(1)))).IsSuccess);
        var revised = Assert.IsType<ControlDraftRevised>(Assert.Single(scenario.PendingEvents));

        // Act
        Assert.True(scenario.When(control => AggregateOutcome.CommitOnSuccess(control.Discard(
            ProgramId, 2, "Withdraw draft", MemberId, "Third display",
            At.AddMinutes(2)))).IsSuccess);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "First display"), created.StoredActor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Second display"), revised.StoredActor);
        Assert.Equal([new ControlDraftDiscarded(TenantId, ProgramId, controlId, 2, MemberId,
                "Third display", "Withdraw draft", At.AddMinutes(2))
            {
                StoredActor = ActorReference.ForMember(MemberId, "Third display"),
            }], scenario.PendingEvents);
    }

    [Fact]
    public void ShouldReplayLegacyEventsWithMemberActorGivenEventsWithoutActor()
    {
        // Arrange
        var controlId = ControlDraft.IdFor(TenantId, ProgramId, "AC-01");
        var created = Deserialize<ControlDraftCreated>($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","control_id":"{{controlId}}","create_request_id":"{{Uuid.CreateVersion4()}}","identifier":"AC-01","content":{{ContentJson}},"actor_member_id":"{{MemberId}}","actor_display":"First display","changed_at":"2026-09-23T12:00:00+00:00"}
            """);
        var revised = Deserialize<ControlDraftRevised>($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","control_id":"{{controlId}}","revision":2,"content":{{ContentJson}},"actor_member_id":"{{MemberId}}","actor_display":"Second display","changed_at":"2026-09-23T12:01:00+00:00"}
            """);
        var discarded = Deserialize<ControlDraftDiscarded>($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","control_id":"{{controlId}}","revision":2,"actor_member_id":"{{MemberId}}","actor_display":"Third display","rationale":"Withdraw draft","discarded_at":"2026-09-23T12:02:00+00:00"}
            """);

        // Act
        var replayed = new AggregateScenario<ControlDraft>(new ControlDraft(TenantId, controlId))
            .Given(DomainEventSeed.Attach(created, controlId, 1),
                DomainEventSeed.Attach(revised, controlId, 2),
                DomainEventSeed.Attach(discarded, controlId, 3)).Aggregate;

        // Assert
        Assert.Equal(2, replayed.Revision);
        Assert.False(replayed.IsVisible);
        Assert.Null(created.StoredActor);
        Assert.Null(revised.StoredActor);
        Assert.Null(discarded.StoredActor);
        Assert.Equal(ActorReference.ForMember(MemberId, "First display"), created.Actor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Second display"), revised.Actor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Third display"), discarded.Actor);
    }

    [Fact]
    public void ShouldSerializeMemberLastChangedByGivenPrePrDraftViewJson()
    {
        // Arrange
        var controlId = Uuid.CreateVersion4();
        var json = $$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","control_id":"{{controlId}}","identifier":"AC-01","revision":2,"status":"draft","owner_resolution":"unresolved","applicability_resolution":"unresolved","content":{{ContentJson}},"last_changed_by_member_id":"{{MemberId}}","last_changed_by_display":"Second display","last_changed_at":"2026-09-23T12:01:00+00:00"}
            """;

        // Act
        var output = JsonNode.Parse(JsonSerializer.Serialize(
            Deserialize<ControlDraftView>(json),
            ComplianceCoreJsonContext.Default.ControlDraftView))!;

        // Assert
        Assert.Equal("member", (string?)output["last_changed_by"]?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)output["last_changed_by"]?["id"]);
        Assert.Equal("Second display", (string?)output["last_changed_by"]?["display"]);
    }

    [Fact]
    public void ShouldSerializeMemberActorGivenPrePrRevisionViewJson()
    {
        // Arrange
        var controlId = Uuid.CreateVersion4();
        var json = $$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","control_id":"{{controlId}}","identifier":"AC-01","revision":1,"content":{{ContentJson}},"changed_by_member_id":"{{MemberId}}","changed_by_display":"First display","changed_at":"2026-09-23T12:00:00+00:00"}
            """;

        // Act
        var output = JsonNode.Parse(JsonSerializer.Serialize(
            Deserialize<ControlDraftRevisionView>(json),
            ComplianceCoreJsonContext.Default.ControlDraftRevisionView))!;

        // Assert
        Assert.Equal("member", (string?)output["actor"]?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)output["actor"]?["id"]);
        Assert.Equal("First display", (string?)output["actor"]?["display"]);
    }

    static T Deserialize<T>(string json) where T : class =>
        Assert.IsType<T>(JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!));

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews access", "The owner reviews access quarterly.",
        ["Dated review record"]);
}
