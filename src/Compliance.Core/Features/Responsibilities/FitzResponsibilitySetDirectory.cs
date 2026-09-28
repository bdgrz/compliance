using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

sealed class FitzResponsibilitySetDirectory(IKvClient client)
    : FitzKvProjectionStore(client, "kv://bdgrz/responsibility-sets-v1/projection",
        "ResponsibilitySetsV1"), IResponsibilitySetDirectory, IResponsibilitySetProjection
{
    public async ValueTask<ResponsibilitySetView?> GetAsync(Uuid tenantId,
        ResponsibilityScope scope, CancellationToken ct = default)
    {
        await using var tx = await BeginReadAsync(tenantId.ToString(), ct).ConfigureAwait(false);
        return await ResponsibilitySetDirectorySchema.Sets.GetAsync(tx,
            ResponsibilitySet.IdFor(tenantId, scope), ct).ConfigureAwait(false);
    }

    public async ValueTask ApplyAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        switch (domainEvent)
        {
            case ResponsibilityAssigned assigned:
                var assignedId = ResponsibilitySet.IdFor(assigned.TenantId, assigned.Scope);
                var current = await ResponsibilitySetDirectorySchema.Sets.GetAsync(Transaction,
                    assignedId, ct).ConfigureAwait(false);
                var assignments = current?.Assignments.ToDictionary(item => item.AssignmentId) ?? [];
                if (assignments.TryGetValue(assigned.AssignmentId, out var prior))
                {
                    if (Equivalent(prior, assigned))
                        return;
                    throw new InvalidOperationException(
                        "A responsibility assignment event was replayed with different terms.");
                }
                assignments[assigned.AssignmentId] = new ResponsibilityAssignmentView(
                    assigned.TenantId, assigned.AssignmentId, assigned.MemberId, assigned.Type,
                    assigned.Scope, assigned.AssignedAt, assigned.AssignedByMemberId,
                    assigned.EffectiveFrom, assigned.EffectiveUntil, null, Uuid.Empty,
                    assigned.SeparationOfDutiesWaiverIds, assigned.AssignedByDisplay);
                await SaveAsync(assigned.TenantId, assignedId, assigned.Scope,
                    current?.Revision ?? 0, assignments,
                    ct).ConfigureAwait(false);
                break;
            case ResponsibilityRevoked revoked:
                var revokedId = ResponsibilitySet.IdFor(revoked.TenantId, revoked.Scope);
                var revokedCurrent = await ResponsibilitySetDirectorySchema.Sets.GetAsync(
                    Transaction, revokedId, ct).ConfigureAwait(false);
                if (revokedCurrent is null || !revokedCurrent.Assignments.Any(item =>
                        item.AssignmentId == revoked.AssignmentId))
                    throw new InvalidOperationException(
                        "A responsibility revocation cannot project before its assignment.");
                var revokedAssignments = revokedCurrent.Assignments.ToDictionary(
                    item => item.AssignmentId);
                var assignment = revokedAssignments[revoked.AssignmentId];
                if (assignment.RevokedAt is { } priorRevokedAt)
                {
                    if (priorRevokedAt == revoked.RevokedAt &&
                        assignment.RevokedByMemberId == revoked.RevokedByMemberId &&
                        StringComparer.Ordinal.Equals(assignment.RevocationReason, revoked.Reason) &&
                        StringComparer.Ordinal.Equals(assignment.RevokedByDisplay, revoked.RevokedByDisplay))
                        return;
                    throw new InvalidOperationException(
                        "A responsibility revocation event was replayed with different terms.");
                }
                revokedAssignments[revoked.AssignmentId] = assignment with
                {
                    RevokedAt = revoked.RevokedAt,
                    RevokedByMemberId = revoked.RevokedByMemberId,
                    RevocationReason = revoked.Reason,
                    RevokedByDisplay = revoked.RevokedByDisplay,
                };
                await SaveAsync(revoked.TenantId, revokedId, revoked.Scope, revokedCurrent.Revision,
                    revokedAssignments, ct).ConfigureAwait(false);
                break;
        }
    }

    async ValueTask SaveAsync(Uuid tenantId, Uuid id, ResponsibilityScope scope, long revision,
        Dictionary<Uuid, ResponsibilityAssignmentView> assignments, CancellationToken ct)
    {
        var view = new ResponsibilitySetView(tenantId, id, scope, checked(revision + 1), assignments.Values
            .OrderBy(item => item.Type)
            .ThenBy(item => item.MemberId.ToString(), StringComparer.Ordinal)
            .ThenBy(item => item.AssignmentId.ToString(), StringComparer.Ordinal).ToArray());
        var current = await ResponsibilitySetDirectorySchema.Sets.GetAsync(Transaction, id, ct)
            .ConfigureAwait(false);
        if (current is null)
            await ResponsibilitySetDirectorySchema.Sets.InsertAsync(Transaction, view, ct)
                .ConfigureAwait(false);
        else
            await ResponsibilitySetDirectorySchema.Sets.ReplaceAsync(Transaction, current, view, ct)
                .ConfigureAwait(false);
    }

    static bool Equivalent(ResponsibilityAssignmentView current, ResponsibilityAssigned incoming) =>
        current.TenantId == incoming.TenantId && current.MemberId == incoming.MemberId &&
        current.Type == incoming.Type && current.Scope == incoming.Scope &&
        current.AssignedAt == incoming.AssignedAt &&
        current.AssignedByMemberId == incoming.AssignedByMemberId &&
        StringComparer.Ordinal.Equals(current.AssignedByDisplay, incoming.AssignedByDisplay) &&
        current.EffectiveFrom == incoming.EffectiveFrom &&
        current.EffectiveUntil == incoming.EffectiveUntil &&
        current.SeparationOfDutiesWaiverIds.SequenceEqual(incoming.SeparationOfDutiesWaiverIds);
}
