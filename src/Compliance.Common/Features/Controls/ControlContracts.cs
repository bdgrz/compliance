using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed record ControlApplicabilityReference(Uuid EntryId, string SubjectType,
    string Subject, Uuid? GovernedRecordId, string Rationale, bool Unresolved);

public sealed record ControlDraftContent(string Title, string Objective, string Description,
    string ImplementationNarrative, IReadOnlyList<string> ExpectedEvidenceDescriptions,
    string? OwnerReference = null,
    IReadOnlyList<ControlApplicabilityReference>? Applicability = null);

public sealed record ControlRegistration(Uuid ControlId, string Identifier, long Revision);

public sealed record ControlDraftView(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string Identifier, long Revision, string Status, string OwnerResolution,
    string ApplicabilityResolution, ControlDraftContent Content,
    Uuid LastChangedByMemberId, string LastChangedByDisplay, DateTimeOffset LastChangedAt);

public sealed record ControlDraftRevisionView(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    string Identifier, long Revision, ControlDraftContent Content,
    Uuid ChangedByMemberId, string ChangedByDisplay, DateTimeOffset ChangedAt);

[Discriminator("bdgrz.control.draft.create", 1)]
public sealed record CreateControlDraft(Uuid TenantId, Uuid ProgramId, string Identifier,
    ControlDraftContent Content) : IRequest<ControlRegistration>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.control.draft.revise", 1)]
public sealed record ReviseControlDraft(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long ExpectedRevision, ControlDraftContent Content) : IRequest,
    IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.control.draft.discard", 1)]
public sealed record DiscardControlDraft(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long ExpectedRevision, string Rationale) : IRequest, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.control.draft.get", 1)]
public sealed record GetControlDraft(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long? MinimumRevision = null) : IRequest<ControlDraftView>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.control.draft.list", 1)]
public sealed record ListControlDrafts(Uuid TenantId, Uuid ProgramId, int? Limit = null,
    string? Cursor = null) : IRequest<Page<ControlDraftView>>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.control.draft.revisions.list", 1)]
public sealed record ListControlDraftRevisions(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    int? Limit = null, string? Cursor = null, long? MinimumControlDraftRevision = null)
    : IRequest<Page<ControlDraftRevisionView>>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.control.draft.revision.get", 1)]
public sealed record GetControlDraftRevision(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long Revision) : IRequest<ControlDraftRevisionView>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.control.draft.created", 1)]
public sealed record ControlDraftCreated(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid CreateRequestId, string Identifier, ControlDraftContent Content, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent;

[Discriminator("bdgrz.control.draft.revised", 1)]
public sealed record ControlDraftRevised(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long Revision, ControlDraftContent Content, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent;

[Discriminator("bdgrz.control.draft.discarded", 1)]
public sealed record ControlDraftDiscarded(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long Revision, Uuid ActorMemberId, string ActorDisplay, string Rationale,
    DateTimeOffset DiscardedAt) : DomainEvent;
