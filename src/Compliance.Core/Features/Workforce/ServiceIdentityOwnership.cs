using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Evaluates whether non-human identities are unowned work as of today.</summary>
public sealed class ServiceIdentityOwnership(IWorkforceObservationDirectoryReader roster,
    IAggregateReader reader, WorkforceObservationReadConsistency consistency, TimeProvider clock)
{
    public async ValueTask<Result<IReadOnlyList<ServiceIdentityView>>> EvaluateAsync(Uuid tenantId,
        IReadOnlyList<ServiceIdentityView> identities, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(identities);
        var ready = await consistency.EnsureCaughtUpAsync(tenantId, ct).ConfigureAwait(false);
        if (!ready.IsSuccess)
            return Result<IReadOnlyList<ServiceIdentityView>>.Failure(ready.Error);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var evaluated = new List<ServiceIdentityView>(identities.Count);
        foreach (var identity in identities)
        {
            IReadOnlyList<string> statuses = [];
            var teamActive = false;
            if (identity.LifecycleStatus != "retired")
            {
                if (identity.OwnerKind == "person")
                    statuses = await roster.ListRelationshipStatusesAsync(tenantId,
                        identity.OwnerId, ct).ConfigureAwait(false);
                else
                    teamActive = (await reader.HydrateAsync(new Team(tenantId, identity.OwnerId),
                        ct).ConfigureAwait(false)).IsActive;
            }
            var reasons = ServiceIdentityAccountability.Evaluate(identity.LifecycleStatus,
                identity.ReviewBy, today, identity.OwnerKind, statuses, teamActive);
            evaluated.Add(identity with
            {
                Unowned = reasons.Count > 0,
                UnownedReasons = reasons,
                Expired = ServiceIdentityAccountability.IsExpired(identity.LifecycleStatus,
                    identity.ExpiresOn, today),
            });
        }
        return Result<IReadOnlyList<ServiceIdentityView>>.Success(evaluated);
    }
}
