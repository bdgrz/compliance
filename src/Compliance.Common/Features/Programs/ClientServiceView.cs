using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed record ClientServiceView(Uuid TenantId, Uuid ServiceId, long Revision,
    string Name, string Purpose, string OwnerReference, string Status,
    Uuid LastChangedByMemberId, string LastChangedByDisplay, DateTimeOffset LastChangedAt,
    Uuid? ProgramId = null)
{
    public ActorReference LastChangedBy => ActorReference.ForMember(
        LastChangedByMemberId, LastChangedByDisplay);
}
