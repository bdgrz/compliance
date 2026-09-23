using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

sealed class FixedOperatorAccess(params Uuid[] userIds) : IPlatformOperatorAccess
{
    public bool AllowAll { get; init; }

    public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) =>
        ValueTask.FromResult(AllowAll || userIds.Contains(userId));
}
