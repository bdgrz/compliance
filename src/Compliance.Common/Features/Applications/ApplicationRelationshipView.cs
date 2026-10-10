using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed record ApplicationRelationshipApproval(Uuid ApproverMemberId,
    ActorReference Approver, long SourceApplicationRevision,
    long TargetApplicationRevision, string ImpactDigest, DateTimeOffset ApprovedAt);

public sealed record ApplicationRelationshipView(Uuid TenantId, Uuid RelationshipId,
    Uuid SourceApplicationId, Uuid TargetApplicationId, string RelationshipType,
    long Revision, string Status, ActorReference ChangedBy, DateTimeOffset ChangedAt,
    long SourceApplicationRevision, long TargetApplicationRevision,
    string? RemovalReason = null, ApplicationRelationshipApproval? Approval = null);
