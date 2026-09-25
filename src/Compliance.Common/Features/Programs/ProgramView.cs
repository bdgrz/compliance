using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed record ProgramView(Uuid TenantId, Uuid ProgramId, string Name, string Stage,
    string? NextStage, long Revision, ProgramPlan Plan, Uuid LastChangedByMemberId,
    string LastChangedByDisplay, DateTimeOffset LastChangedAt,
    IReadOnlyList<ProgramStageView> StagePlan)
{
    public ActorReference LastChangedBy => ActorReference.ForMember(
        LastChangedByMemberId, LastChangedByDisplay);
}
