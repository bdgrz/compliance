using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class DraftActorSnapshotTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly DateTimeOffset At = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldKeepControlActorGivenDisplayChangeAndLegacyReplay()
    {
        // Arrange
        var content = new ControlDraftContent("Access review", "Review access",
            "Management reviews access", "The owner reviews access quarterly.",
            ["Dated review record"]);
        var controlId = ControlDraft.IdFor(TenantId, ProgramId, "AC-01");
        var control = new ControlDraft(TenantId, controlId);
        Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), "AC-01", content,
            MemberId, "First display", At).IsSuccess);
        Assert.True(control.Revise(ProgramId, 1, content with { Title = "Updated review" },
            MemberId, "Second display", At.AddMinutes(1)).IsSuccess);
        Assert.True(control.Discard(ProgramId, 2, "Withdraw draft", MemberId,
            "Third display", At.AddMinutes(2)).IsSuccess);

        // Act
        var events = new AggregateScenario<ControlDraft>(control).PendingEvents;
        var created = Assert.IsType<ControlDraftCreated>(events[0]);
        var revised = Assert.IsType<ControlDraftRevised>(events[1]);
        var discarded = Assert.IsType<ControlDraftDiscarded>(events[2]);
        var legacyCreated = WithoutActor(created,
            ComplianceCoreJsonContext.Default.ControlDraftCreated);
        var legacyRevised = WithoutActor(revised,
            ComplianceCoreJsonContext.Default.ControlDraftRevised);
        var legacyDiscarded = WithoutActor(discarded,
            ComplianceCoreJsonContext.Default.ControlDraftDiscarded);
        var replayed = new AggregateScenario<ControlDraft>(new ControlDraft(TenantId, controlId))
            .Given(DomainEventSeed.Attach(legacyCreated, controlId, 1),
                DomainEventSeed.Attach(legacyRevised, controlId, 2),
                DomainEventSeed.Attach(legacyDiscarded, controlId, 3)).Aggregate;
        var current = new ControlDraftView(TenantId, ProgramId, controlId, "AC-01", 2,
            "draft", "unresolved", "unresolved", content, MemberId, "Second display", At);
        var history = new ControlDraftRevisionView(TenantId, ProgramId, controlId,
            "AC-01", 1, content, MemberId, "First display", At);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "First display"), created.StoredActor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Second display"), revised.StoredActor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Third display"), discarded.StoredActor);
        Assert.Equal(created.Actor, legacyCreated.Actor);
        Assert.Equal(revised.Actor, legacyRevised.Actor);
        Assert.Equal(discarded.Actor, legacyDiscarded.Actor);
        Assert.Null(legacyCreated.StoredActor);
        Assert.Null(legacyRevised.StoredActor);
        Assert.Null(legacyDiscarded.StoredActor);
        Assert.Equal(revised.Actor, WithoutActor(current,
            ComplianceCoreJsonContext.Default.ControlDraftView, "last_changed_by").LastChangedBy);
        Assert.Equal(created.Actor, WithoutActor(history,
            ComplianceCoreJsonContext.Default.ControlDraftRevisionView).Actor);
        Assert.False(control.IsVisible);
        Assert.Equal(2, replayed.Revision);
        Assert.False(replayed.IsVisible);
    }

    [Fact]
    public void ShouldKeepCommitmentActorGivenDisplayChangeAndLegacyReplay()
    {
        // Arrange
        var serviceId = Uuid.CreateVersion4();
        var draftId = CommitmentDraft.IdFor(TenantId, ProgramId, "service_commitment", "SC-01");
        var draft = new CommitmentDraft(TenantId, draftId);
        Assert.True(draft.Create(ProgramId, Uuid.CreateVersion4(), serviceId,
            "service_commitment", "SC-01", "First statement", "Draft context", "Source A",
            MemberId, "First display", At).IsSuccess);
        Assert.True(draft.Revise(ProgramId, 1, "Second statement", "Draft context",
            "Source B", MemberId, "Second display", At.AddMinutes(1)).IsSuccess);

        // Act
        var events = new AggregateScenario<CommitmentDraft>(draft).PendingEvents;
        var created = Assert.IsType<CommitmentDraftCreated>(events[0]);
        var revised = Assert.IsType<CommitmentDraftRevised>(events[1]);
        var legacyCreated = WithoutActor(created,
            ComplianceCoreJsonContext.Default.CommitmentDraftCreated);
        var legacyRevised = WithoutActor(revised,
            ComplianceCoreJsonContext.Default.CommitmentDraftRevised);
        var replayed = new AggregateScenario<CommitmentDraft>(new CommitmentDraft(TenantId, draftId))
            .Given(DomainEventSeed.Attach(legacyCreated, draftId, 1),
                DomainEventSeed.Attach(legacyRevised, draftId, 2)).Aggregate;
        var current = new CommitmentDraftView(TenantId, ProgramId, draftId, serviceId,
            "service_commitment", "SC-01", 2, "draft", "resolved", "unresolved",
            "unresolved", "Second statement", "Draft context", "Source B", MemberId,
            "Second display", At.AddMinutes(1));
        var history = new CommitmentDraftRevisionView(TenantId, ProgramId, draftId, serviceId,
            "service_commitment", "SC-01", 1, "First statement", "Draft context",
            "Source A", MemberId, "First display", At);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "First display"), created.StoredActor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Second display"), revised.StoredActor);
        Assert.Equal(created.Actor, legacyCreated.Actor);
        Assert.Equal(revised.Actor, legacyRevised.Actor);
        Assert.Null(legacyCreated.StoredActor);
        Assert.Null(legacyRevised.StoredActor);
        Assert.Equal(revised.Actor, WithoutActor(current,
            ComplianceCoreJsonContext.Default.CommitmentDraftView, "last_changed_by")
            .LastChangedBy);
        Assert.Equal(created.Actor, WithoutActor(history,
            ComplianceCoreJsonContext.Default.CommitmentDraftRevisionView).Actor);
        Assert.Equal(2, draft.Revision);
        Assert.True(replayed.IsCreated);
        Assert.Equal(2, replayed.Revision);
    }

    [Fact]
    public void ShouldKeepRiskActorGivenDisplayChangeAndLegacyReplay()
    {
        // Arrange
        var content = new RiskDraftContent("Provider outage", "Provider unavailable",
            "Requests cannot be processed", "Management note");
        var riskId = RiskDraft.IdFor(TenantId, ProgramId, "R-01");
        var risk = new RiskDraft(TenantId, riskId);
        Assert.True(risk.Create(ProgramId, Uuid.CreateVersion4(), "R-01", content,
            MemberId, "First display", At).IsSuccess);
        Assert.True(risk.Revise(ProgramId, 1, content with { Scenario = "Extended outage" },
            MemberId, "Second display", At.AddMinutes(1)).IsSuccess);

        // Act
        var events = new AggregateScenario<RiskDraft>(risk).PendingEvents;
        var created = Assert.IsType<RiskDraftCreated>(events[0]);
        var revised = Assert.IsType<RiskDraftRevised>(events[1]);
        var legacyCreated = WithoutActor(created,
            ComplianceCoreJsonContext.Default.RiskDraftCreated);
        var legacyRevised = WithoutActor(revised,
            ComplianceCoreJsonContext.Default.RiskDraftRevised);
        var replayed = new AggregateScenario<RiskDraft>(new RiskDraft(TenantId, riskId))
            .Given(DomainEventSeed.Attach(legacyCreated, riskId, 1),
                DomainEventSeed.Attach(legacyRevised, riskId, 2)).Aggregate;
        var current = new RiskDraftView(TenantId, ProgramId, riskId, "R-01", 2,
            "draft", "unresolved", content, MemberId, "Second display", At.AddMinutes(1));
        var history = new RiskDraftRevisionView(TenantId, ProgramId, riskId, "R-01", 1,
            content, MemberId, "First display", At);

        // Assert
        Assert.Equal(ActorReference.ForMember(MemberId, "First display"), created.StoredActor);
        Assert.Equal(ActorReference.ForMember(MemberId, "Second display"), revised.StoredActor);
        Assert.Equal(created.Actor, legacyCreated.Actor);
        Assert.Equal(revised.Actor, legacyRevised.Actor);
        Assert.Null(legacyCreated.StoredActor);
        Assert.Null(legacyRevised.StoredActor);
        Assert.Equal(revised.Actor, WithoutActor(current,
            ComplianceCoreJsonContext.Default.RiskDraftView, "last_changed_by").LastChangedBy);
        Assert.Equal(created.Actor, WithoutActor(history,
            ComplianceCoreJsonContext.Default.RiskDraftRevisionView).Actor);
        Assert.Equal(2, risk.Revision);
        Assert.True(replayed.IsCreated);
        Assert.Equal(2, replayed.Revision);
    }

    static T WithoutActor<T>(T value, JsonTypeInfo<T> typeInfo, string field = "actor")
        where T : notnull
    {
        var json = Assert.IsType<JsonObject>(JsonNode.Parse(JsonSerializer.Serialize(value,
            typeInfo)));
        Assert.True(json.Remove(field));
        return Assert.IsType<T>(JsonSerializer.Deserialize(json.ToJsonString(), typeInfo));
    }
}
