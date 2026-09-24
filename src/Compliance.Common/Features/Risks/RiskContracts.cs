using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

public sealed record RiskDraftContent(string Title, string Scenario,
    string PotentialEffect, string? SourceNote);

public sealed record RiskRegistration(Uuid RiskId, string Identifier, long Revision);

public sealed record RiskDraftView(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    string Identifier, long Revision, string Status, string OwnerResolution,
    RiskDraftContent Content,
    Uuid LastChangedByMemberId, string LastChangedByDisplay, DateTimeOffset LastChangedAt)
{
    readonly ActorReference? _lastChangedBy;

    /// <summary>The snapshotted actor; pre-snapshot rows fall back to the member columns.</summary>
    [JsonPropertyName("last_changed_by")]
    public ActorReference LastChangedBy
    {
        get => _lastChangedBy ?? ActorReference.ForMember(LastChangedByMemberId,
            LastChangedByDisplay);
        init => _lastChangedBy = value;
    }
}

public sealed record RiskDraftRevisionView(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    string Identifier, long Revision, RiskDraftContent Content,
    Uuid ChangedByMemberId, string ChangedByDisplay, DateTimeOffset ChangedAt)
{
    readonly ActorReference? _actor;

    /// <summary>The snapshotted actor; pre-snapshot rows fall back to the member columns.</summary>
    [JsonPropertyName("actor")]
    public ActorReference Actor
    {
        get => _actor ?? ActorReference.ForMember(ChangedByMemberId, ChangedByDisplay);
        init => _actor = value;
    }
}

[Discriminator("bdgrz.risk.draft.create", 1)]
public sealed record CreateRiskDraft(Uuid TenantId, Uuid ProgramId, string Identifier,
    string Title, string Scenario, string PotentialEffect, string? SourceNote = null)
    : IRequest<RiskRegistration>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.risk.draft.revise", 1)]
public sealed record ReviseRiskDraft(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long ExpectedRevision, string Title, string Scenario, string PotentialEffect,
    string? SourceNote = null) : IRequest,
    IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.risk.draft.get", 1)]
public sealed record GetRiskDraft(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long? MinimumRevision = null) : IRequest<RiskDraftView>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.risk.draft.list", 1)]
public sealed record ListRiskDrafts(Uuid TenantId, Uuid ProgramId, int? Limit = null,
    string? Cursor = null) : IRequest<Page<RiskDraftView>>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.risk.draft.revision.get", 1)]
public sealed record GetRiskDraftRevision(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision) : IRequest<RiskDraftRevisionView>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.risk.draft.revisions.list", 1)]
public sealed record ListRiskDraftRevisions(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    int? Limit = null, string? Cursor = null, long? MinimumRiskRevision = null)
    : IRequest<Page<RiskDraftRevisionView>>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.risk.draft.created", 1)]
public sealed record RiskDraftCreated(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    Uuid CreateRequestId, string Identifier, RiskDraftContent Content, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}

[Discriminator("bdgrz.risk.draft.revised", 1)]
public sealed record RiskDraftRevised(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, RiskDraftContent Content, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
