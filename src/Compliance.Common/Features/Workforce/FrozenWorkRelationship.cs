using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     A work relationship exactly as accepted when a roster snapshot was frozen.
///     <c>ManagerPersonId</c> is the restricted manager chain and is null when redacted.
/// </summary>
public sealed record FrozenWorkRelationship(Uuid RelationshipId, long Revision, Uuid PersonId,
    string SourceWorkerId, string WorkerType, string LifecycleStatus, DateOnly StartDate,
    DateOnly? EndDate, string? Department, Uuid? ManagerPersonId, Uuid? SponsorPersonId);
