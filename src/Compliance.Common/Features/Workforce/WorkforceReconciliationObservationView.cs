using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     A roster reconciliation finding evaluated from the governed manual roster and tenant
///     memberships when read. <c>Kind</c> is <c>missing</c>, <c>duplicate</c>, <c>conflicting</c>,
///     <c>stale</c>, or <c>access_only</c>, and <c>Reason</c> names the rule. The ID is stable for
///     the same underlying facts, so a resolution stays attached until those facts change.
///     Observations are compliance work only; they never grant or revoke access.
/// </summary>
public sealed record WorkforceReconciliationObservationView(Uuid TenantId, Uuid ObservationId,
    string Kind, string Reason, string Status, IReadOnlyList<Uuid> PersonIds,
    IReadOnlyList<Uuid> RelationshipIds, IReadOnlyList<Uuid> UserIds,
    WorkforceObservationResolutionView? Resolution = null);
