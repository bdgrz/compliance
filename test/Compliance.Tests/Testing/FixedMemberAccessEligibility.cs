using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

sealed class FixedMemberAccessEligibility(bool eligible) : IMemberAccessEligibility
{
    public ValueTask<bool> IsEligibleAsync(Uuid tenantId, Uuid userId,
        CancellationToken ct = default) => ValueTask.FromResult(eligible);
}
