using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed record ClientServiceRevisionView(Uuid ServiceId, long Revision,
    string Name, string Purpose, string OwnerReference, string Status,
    string? RetirementRationale, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt, Uuid? ProgramId = null)
{
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
