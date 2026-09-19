using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public sealed record BoundaryScopeEntry(Uuid EntryId, string Kind, string SubjectType,
    string Subject, Uuid? GovernedRecordId, string OwnerReference,
    string Rationale, bool Unresolved);

public sealed record BoundaryContent(string Statement, string EngagementStage,
    IReadOnlyList<string> TrustServicesCategories, IReadOnlyList<BoundaryScopeEntry> Entries);

public sealed record BoundaryRegistration(Uuid BoundaryId, Uuid DraftVersionId);

public sealed record BoundaryDecisionView(Uuid TenantId, Uuid BoundaryId,
    Uuid DecisionId, Uuid VersionId, long Revision, string Outcome,
    Uuid ActorMemberId, string ActorDisplay, string Rationale,
    DateTimeOffset DecidedAt, Uuid? SupersedesDecisionId,
    Uuid? ReliesOnDecisionId, string? ImpactDigest);

public sealed record BoundaryVersionView(Uuid TenantId, Uuid BoundaryId, Uuid ProgramId,
    Uuid VersionId, long Revision, BoundaryContent Content, string Status,
    DateOnly? EffectiveFrom, Uuid AuthorMemberId, string AuthorDisplay,
    DateTimeOffset ChangedAt);

public sealed record BoundaryView(Uuid TenantId, Uuid BoundaryId, Uuid ProgramId,
    BoundaryVersionView? Draft, BoundaryVersionView? LatestApprovedVersion,
    BoundaryDecisionView? LatestDecision);

public sealed record BoundaryChange(string Field, string ChangeType, Uuid? EntryId,
    string? PreviousValue, string? ProposedValue,
    BoundaryScopeEntry? PreviousEntry, BoundaryScopeEntry? ProposedEntry);

public sealed record BoundaryAffectedRecord(string Context, string RecordType,
    Uuid RecordId, string Reason);

public sealed record BoundaryImpactContribution(string Context,
    IReadOnlyList<BoundaryAffectedRecord> Records, bool Complete);

public sealed record BoundaryImpactPreview(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long Revision, Uuid? ApprovedVersionId,
    IReadOnlyList<BoundaryChange> Changes,
    IReadOnlyList<BoundaryImpactContribution> Contributions,
    IReadOnlyList<string> PendingContexts, bool Complete, string Digest);

public interface IBoundaryAuthoringRequest : IProgramManagementRequest;

[Discriminator("bdgrz.boundary.create", 1)]
public sealed record CreateBoundary(Uuid TenantId, Uuid ProgramId, BoundaryContent Content)
    : IRequest<BoundaryRegistration>, IBoundaryAuthoringRequest, ICallable;

[Discriminator("bdgrz.boundary.draft.revise", 1)]
public sealed record ReviseBoundaryDraft(Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long ExpectedRevision, BoundaryContent Content)
    : IRequest, IBoundaryAuthoringRequest, ICallable;

[Discriminator("bdgrz.boundary.review", 1)]
public sealed record ReviewBoundary(Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long ExpectedRevision, string Outcome, string Rationale)
    : IRequest, IBoundaryAuthoringRequest, ICallable;

[Discriminator("bdgrz.boundary.approve", 1)]
public sealed record ApproveBoundary(Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long ExpectedRevision, Uuid AcceptedReviewDecisionId, DateOnly EffectiveFrom,
    string Rationale, string ImpactDigest) : IRequest, IBoundaryAuthoringRequest, ICallable;

[Discriminator("bdgrz.boundary.successor.propose", 1)]
public sealed record ProposeBoundarySuccessor(Uuid TenantId, Uuid BoundaryId,
    Uuid ExpectedApprovedVersionId, BoundaryContent Content)
    : IRequest<BoundaryRegistration>, IBoundaryAuthoringRequest, ICallable;

[Discriminator("bdgrz.boundary.get", 1)]
public sealed record GetBoundary(Uuid TenantId, Uuid BoundaryId)
    : IRequest<BoundaryView>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.boundary.version.get", 1)]
public sealed record GetBoundaryVersion(Uuid TenantId, Uuid BoundaryId, Uuid VersionId)
    : IRequest<BoundaryVersionView>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.boundary.versions.list", 1)]
public sealed record ListBoundaryVersions(Uuid TenantId, Uuid BoundaryId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<BoundaryVersionView>>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.boundary.version.effective.get", 1)]
public sealed record GetEffectiveBoundaryVersion(Uuid TenantId, Uuid BoundaryId,
    DateOnly EffectiveOn)
    : IRequest<BoundaryVersionView>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.boundary.decision.get", 1)]
public sealed record GetBoundaryDecision(Uuid TenantId, Uuid BoundaryId, Uuid DecisionId)
    : IRequest<BoundaryDecisionView>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.boundary.decisions.list", 1)]
public sealed record ListBoundaryDecisions(Uuid TenantId, Uuid BoundaryId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<BoundaryDecisionView>>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.boundary.impact.preview", 1)]
public sealed record PreviewBoundaryImpact(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long ExpectedRevision)
    : IRequest<BoundaryImpactPreview>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.boundary.draft.created", 1)]
public sealed record BoundaryDraftCreated(Uuid TenantId, Uuid BoundaryId, Uuid ProgramId,
    Uuid DraftVersionId, BoundaryContent Content, Uuid AuthorMemberId,
    string AuthorDisplay, DateTimeOffset ChangedAt) : DomainEvent;

[Discriminator("bdgrz.boundary.draft.revised", 1)]
public sealed record BoundaryDraftRevised(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, long Revision, BoundaryContent Content, Uuid AuthorMemberId,
    string AuthorDisplay, DateTimeOffset ChangedAt) : DomainEvent;

[Discriminator("bdgrz.boundary.reviewed", 1)]
public sealed record BoundaryReviewed(Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long Revision, Uuid DecisionId, string Outcome, Uuid ActorMemberId,
    string ActorDisplay, string Rationale, DateTimeOffset DecidedAt) : DomainEvent;

[Discriminator("bdgrz.boundary.approved", 1)]
public sealed record BoundaryApproved(Uuid TenantId, Uuid BoundaryId, Uuid DraftVersionId,
    long Revision, Uuid ApprovalDecisionId, Uuid AcceptedReviewDecisionId, Uuid ActorMemberId,
    string ActorDisplay, string Rationale, DateOnly EffectiveFrom,
    DateTimeOffset DecidedAt, string ImpactDigest) : DomainEvent;

[Discriminator("bdgrz.boundary.successor.proposed", 1)]
public sealed record BoundarySuccessorProposed(Uuid TenantId, Uuid BoundaryId,
    Uuid DraftVersionId, Uuid PredecessorVersionId, BoundaryContent Content,
    Uuid AuthorMemberId, string AuthorDisplay, DateTimeOffset ChangedAt) : DomainEvent;
