using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

sealed class FixedMemberAccessEligibility(bool eligible) : IMemberAccessEligibility
{
    public Uuid MembershipEpisodeId { get; init; } = Uuid.Empty;

    public ValueTask<bool> IsEligibleAsync(Uuid tenantId, Uuid userId,
        CancellationToken ct = default) => ValueTask.FromResult(eligible);

    public ValueTask<Uuid?> GetMembershipEpisodeIdAsync(Uuid tenantId, Uuid memberId,
        CancellationToken ct = default) =>
        ValueTask.FromResult<Uuid?>(eligible ? MembershipEpisodeId : null);
}
