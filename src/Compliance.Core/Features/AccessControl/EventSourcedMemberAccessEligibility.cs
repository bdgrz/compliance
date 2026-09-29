using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Checks member events before the membership projection can catch up.</summary>
sealed class EventSourcedMemberAccessEligibility(IAggregateReader reader) : IMemberAccessEligibility
{
    public async ValueTask<bool> IsEligibleAsync(Uuid tenantId, Uuid userId,
        CancellationToken ct = default)
    {
        if (tenantId == Uuid.Empty || userId == Uuid.Empty)
            return false;
        var member = await reader.HydrateAsync(new Member(tenantId, userId), ct).ConfigureAwait(false);
        return member.IsRegistered && member.Affiliation == "client_personnel" && !member.IsSuspended;
    }
}
