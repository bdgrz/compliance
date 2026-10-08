using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A denial-only wall over the client's complete, retained actual assignment history.</summary>
public sealed class ClientManagementIndependenceGuard(IAggregateReader reader)
{
    public async ValueTask<bool> CanAuthorAsync(Uuid tenantId, Uuid canonicalUserId, CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || canonicalUserId == Uuid.Empty)
            return false;
        var ledger = await reader.HydrateAsync(new IndependenceLedger(tenantId), ct).ConfigureAwait(false);
        return !ledger.ActualAssignmentHistory.Any(assignment => assignment.UserId == canonicalUserId &&
            assignment.Practice == EngagementPractice.Attest);
    }
}
