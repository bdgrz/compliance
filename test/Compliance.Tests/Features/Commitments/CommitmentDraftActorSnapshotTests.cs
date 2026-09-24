using System.Text.Json;
using System.Text.Json.Nodes;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Commitments;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

public sealed class CommitmentDraftActorSnapshotTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ServiceId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly DateTimeOffset At = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldSnapshotEachActorDisplayGivenDisplayChangesBetweenEvents()
    {
        // Arrange
        var draftId = CommitmentDraft.IdFor(TenantId, ProgramId, "service_commitment", "SC-01");
        var draft = new CommitmentDraft(TenantId, draftId);
        Assert.True(draft.Create(ProgramId, Uuid.CreateVersion4(), ServiceId,
            "service_commitment", "SC-01", "First statement", "Draft context", "Source A",
            MemberId, "First display", At).IsSuccess);
        Assert.True(draft.Revise(ProgramId, 1, "Second statement", "Draft context",
            "Source B", MemberId, "Second display", At.AddMinutes(1)).IsSuccess);

        // Act
        var events = new AggregateScenario<CommitmentDraft>(draft).PendingEvents;

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "First display"),
            Assert.IsType<CommitmentDraftCreated>(events[0]).StoredActor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Second display"),
            Assert.IsType<CommitmentDraftRevised>(events[1]).StoredActor);
    }

    [Fact]
    public void ShouldReplayLegacyEventsWithMemberActorGivenEventsWithoutActor()
    {
        // Arrange
        var draftId = CommitmentDraft.IdFor(TenantId, ProgramId, "service_commitment", "SC-01");
        var created = Deserialize<CommitmentDraftCreated>($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","draft_id":"{{draftId}}","create_request_id":"{{Uuid.CreateVersion4()}}","service_id":"{{ServiceId}}","kind":"service_commitment","identifier":"SC-01","statement":"First statement","context":"Draft context","source_reference":"Source A","actor_member_id":"{{MemberId}}","actor_display":"First display","changed_at":"2026-09-23T12:00:00+00:00"}
            """);
        var revised = Deserialize<CommitmentDraftRevised>($$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","draft_id":"{{draftId}}","revision":2,"statement":"Second statement","context":"Draft context","source_reference":"Source B","actor_member_id":"{{MemberId}}","actor_display":"Second display","changed_at":"2026-09-23T12:01:00+00:00"}
            """);

        // Act
        var replayed = new AggregateScenario<CommitmentDraft>(
                new CommitmentDraft(TenantId, draftId))
            .Given(DomainEventSeed.Attach(created, draftId, 1),
                DomainEventSeed.Attach(revised, draftId, 2)).Aggregate;

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
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","draft_id":"{{Uuid.CreateVersion4()}}","service_id":"{{ServiceId}}","kind":"service_commitment","identifier":"SC-01","revision":2,"status":"draft","source_resolution":"resolved","owner_resolution":"unresolved","applicability_resolution":"unresolved","statement":"Second statement","context":"Draft context","source_reference":"Source B","last_changed_by_member_id":"{{MemberId}}","last_changed_by_display":"Second display","last_changed_at":"2026-09-23T12:01:00+00:00"}
            """;

        // Act
        var output = JsonNode.Parse(JsonSerializer.Serialize(
            Deserialize<CommitmentDraftView>(json),
            ComplianceCoreJsonContext.Default.CommitmentDraftView))!;

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
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","draft_id":"{{Uuid.CreateVersion4()}}","service_id":"{{ServiceId}}","kind":"service_commitment","identifier":"SC-01","revision":1,"statement":"First statement","context":"Draft context","source_reference":"Source A","changed_by_member_id":"{{MemberId}}","changed_by_display":"First display","changed_at":"2026-09-23T12:00:00+00:00"}
            """;

        // Act
        var output = JsonNode.Parse(JsonSerializer.Serialize(
            Deserialize<CommitmentDraftRevisionView>(json),
            ComplianceCoreJsonContext.Default.CommitmentDraftRevisionView))!;

        // Assert
        Assert.Equal("member", (string?)output["actor"]?["kind"]);
        Assert.Equal(MemberId.ToString(), (string?)output["actor"]?["id"]);
        Assert.Equal("First display", (string?)output["actor"]?["display"]);
    }

    static T Deserialize<T>(string json) where T : class =>
        Assert.IsType<T>(JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))!));
}
