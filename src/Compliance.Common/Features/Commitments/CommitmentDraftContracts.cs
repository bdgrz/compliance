using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments;

public sealed record CommitmentDraftRegistration(Uuid DraftId, string Kind,
    string Identifier, long Revision);

public sealed record CommitmentDraftView(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    Uuid ServiceId, string Kind, string Identifier, long Revision, string Status,
    string SourceResolution, string OwnerResolution, string ApplicabilityResolution,
    string Statement, string Context, string SourceReference,
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

public sealed record CommitmentDraftRevisionView(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    Uuid ServiceId, string Kind, string Identifier, long Revision,
    string Statement, string Context, string SourceReference,
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

[Discriminator("bdgrz.commitment.draft.create", 1)]
public sealed record CreateCommitmentDraft(Uuid TenantId, Uuid ProgramId, Uuid ServiceId,
    string Kind, string Identifier, string Statement, string Context, string SourceReference)
    : IRequest<CommitmentDraftRegistration>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.commitment.draft.revise", 1)]
public sealed record ReviseCommitmentDraft(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long ExpectedRevision, string Statement, string Context, string SourceReference)
    : IRequest, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.commitment.draft.get", 1)]
public sealed record GetCommitmentDraft(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long? MinimumRevision = null)
    : IRequest<CommitmentDraftView>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.commitment.draft.list", 1)]
public sealed record ListCommitmentDrafts(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<CommitmentDraftView>>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.commitment.draft.revision.get", 1)]
public sealed record GetCommitmentDraftRevision(Uuid TenantId, Uuid ProgramId,
    Uuid DraftId, long Revision)
    : IRequest<CommitmentDraftRevisionView>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.commitment.draft.revision.list", 1)]
public sealed record ListCommitmentDraftRevisions(Uuid TenantId, Uuid ProgramId,
    Uuid DraftId, int? Limit = null, string? Cursor = null,
    long? MinimumDraftRevision = null)
    : IRequest<Page<CommitmentDraftRevisionView>>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.commitment.draft.created", 1)]
public sealed record CommitmentDraftCreated(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    Uuid CreateRequestId, Uuid ServiceId, string Kind, string Identifier,
    string Statement, string Context, string SourceReference,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}

[Discriminator("bdgrz.commitment.draft.revised", 1)]
public sealed record CommitmentDraftRevised(Uuid TenantId, Uuid ProgramId, Uuid DraftId,
    long Revision, string Statement, string Context, string SourceReference,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
