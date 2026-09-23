using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

sealed class EventSourcedPlatformOperatorAccess(IAggregateReader reader,
    PlatformOperatorAuthority configuration) : IPlatformOperatorAccess
{
    public async ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default)
    {
        if (userId == Uuid.Empty)
            return false;
        if (configuration.DeveloperAuthentication)
            return true;
        var roster = await reader.HydrateAsync(new PlatformOperatorRoster(), ct).ConfigureAwait(false);
        return roster.IsOperator(userId);
    }
}
