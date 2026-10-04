using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public interface IMemberAccessEligibility
{
    ValueTask<bool> IsEligibleAsync(Uuid tenantId, Uuid userId, CancellationToken ct = default);

    ValueTask<Uuid?> GetMembershipEpisodeIdAsync(Uuid tenantId, Uuid memberId,
        CancellationToken ct = default) => ValueTask.FromResult<Uuid?>(null);
}
