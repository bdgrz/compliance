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
    Uuid LastChangedByMemberId, string LastChangedByDisplay, DateTimeOffset LastChangedAt);

public sealed record RiskDraftRevisionView(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    string Identifier, long Revision, RiskDraftContent Content,
    Uuid ChangedByMemberId, string ChangedByDisplay, DateTimeOffset ChangedAt);

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

[Discriminator("bdgrz.risk.draft.created", 1)]
public sealed record RiskDraftCreated(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    Uuid CreateRequestId, string Identifier, RiskDraftContent Content, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent;

[Discriminator("bdgrz.risk.draft.revised", 1)]
public sealed record RiskDraftRevised(Uuid TenantId, Uuid ProgramId, Uuid RiskId,
    long Revision, RiskDraftContent Content, Uuid ActorMemberId,
    string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent;
