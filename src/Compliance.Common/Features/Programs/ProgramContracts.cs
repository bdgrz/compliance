using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed record ProgramPlan(DateOnly? TargetReadinessDate, DateOnly? TargetTypeIAsOfDate,
    DateOnly? TargetTypeIIStartDate, DateOnly? TargetTypeIIEndDate,
    string? ReadinessAdvisor, string? AuditFirm);

public sealed record ProgramRegistration(Uuid ProgramId);

public sealed record ProgramStageView(string Stage, string AdvanceWhen);

public sealed record ProgramRevisionView(Uuid ProgramId, long Revision, string Name,
    ProgramPlan Plan, Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt)
{
    public ActorReference Actor => ActorReference.ForMember(ActorMemberId, ActorDisplay);
}

public sealed record ProgramView(Uuid TenantId, Uuid ProgramId, string Name, string Stage,
    string? NextStage, long Revision, ProgramPlan Plan, Uuid LastChangedByMemberId,
    string LastChangedByDisplay, DateTimeOffset LastChangedAt,
    IReadOnlyList<ProgramStageView> StagePlan)
{
    public ActorReference LastChangedBy => ActorReference.ForMember(
        LastChangedByMemberId, LastChangedByDisplay);
}

public interface IProgramManagementRequest : IRequestBase
{
    Uuid TenantId { get; }
}

[Discriminator("bdgrz.program.create", 1)]
public sealed record CreateProgram(Uuid TenantId, string Name, ProgramPlan Plan)
    : IRequest<ProgramRegistration>, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.program.revise", 1)]
public sealed record ReviseProgram(Uuid TenantId, Uuid ProgramId, long ExpectedRevision,
    string Name, ProgramPlan Plan) : IRequest, IProgramManagementRequest, ICallable;

[Discriminator("bdgrz.program.get", 1)]
public sealed record GetProgram(Uuid TenantId, Uuid ProgramId, long? MinimumRevision = null)
    : IRequest<ProgramView>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.program.list", 1)]
public sealed record ListPrograms(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ProgramView>>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.program.revisions.list", 1)]
public sealed record ListProgramRevisions(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null, long? MinimumProgramRevision = null)
    : IRequest<Page<ProgramRevisionView>>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.program.revision.get", 1)]
public sealed record GetProgramRevision(Uuid TenantId, Uuid ProgramId, long Revision)
    : IRequest<ProgramRevisionView>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.program.created", 1)]
public sealed record ProgramCreated(Uuid TenantId, Uuid ProgramId, string Name, ProgramPlan Plan,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}

[Discriminator("bdgrz.program.revised", 1)]
public sealed record ProgramRevised(Uuid TenantId, Uuid ProgramId, long Revision,
    string Name, ProgramPlan Plan, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt) : DomainEvent
{
    [JsonPropertyName("actor")]
    public ActorReference? StoredActor { get; init; }

    [JsonIgnore]
    public ActorReference Actor => StoredActor ?? ActorReference.ForMember(ActorMemberId, ActorDisplay);
}
