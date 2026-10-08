using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>One immutable capture of a person's authoritative membership correlation for this evaluation.</summary>
public sealed record PolicyAcknowledgementPersonSnapshot(Uuid TenantId, Uuid PersonId, bool IsCreated,
    long Revision, ulong CommittedStreamPosition, Uuid? CorrelatedUserId)
{
    public static PolicyAcknowledgementPersonSnapshot Capture(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);
        return new(Uuid.Parse(person.Stream.Realm, null), person.Id, person.IsCreated, person.Revision,
            person.CommittedStreamPosition, person.CorrelatedUserId);
    }
}
