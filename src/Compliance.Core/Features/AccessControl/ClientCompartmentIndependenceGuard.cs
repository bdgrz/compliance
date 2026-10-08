using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A denial-only compartment wall over complete retained actual assignments; ordinary grants remain mandatory.</summary>
public sealed class ClientCompartmentIndependenceGuard(IAggregateReader reader)
{
    public async ValueTask<bool> CanReadAsync(Uuid tenantId, Uuid canonicalUserId,
        RecordCompartment compartment, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || canonicalUserId == Uuid.Empty || !Enum.IsDefined(compartment))
            return false;
        var ledger = await reader.HydrateAsync(new IndependenceLedger(tenantId), ct).ConfigureAwait(false);
        return !IndependenceCompartments.IsBlockedByWall(tenantId,
            ledger.ActualAssignmentHistory.Where(assignment => assignment.UserId == canonicalUserId), compartment);
    }
}
