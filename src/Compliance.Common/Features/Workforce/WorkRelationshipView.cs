using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     A person's work relationship from the tenant's governed roster. Lists return
///     <c>ManagerPersonId</c> as null and <c>RestrictedFieldsRedacted</c> as true.
/// </summary>
public sealed record WorkRelationshipView(Uuid TenantId, Uuid RelationshipId, long Revision,
    Uuid PersonId, string SourceWorkerId, string WorkerType, string LifecycleStatus,
    DateOnly StartDate, DateOnly? EndDate, string? Department, Uuid? ManagerPersonId,
    Uuid? SponsorPersonId, bool RestrictedFieldsRedacted, string SourceKind,
    ActorReference LastChangedBy, DateTimeOffset LastChangedAt);
