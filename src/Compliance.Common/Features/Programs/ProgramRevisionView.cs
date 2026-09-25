using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed record ProgramRevisionView(Uuid ProgramId, long Revision, string Name,
    ProgramPlan Plan, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt)
{
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
