using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed record ProgramPlan(DateOnly? TargetReadinessDate, DateOnly? TargetTypeIAsOfDate,
    DateOnly? TargetTypeIIStartDate, DateOnly? TargetTypeIIEndDate,
    string? ReadinessAdvisor, string? AuditFirm);

public sealed record ProgramRegistration(Uuid ProgramId);

public sealed record ProgramView(Uuid TenantId, Uuid ProgramId, string Name, string Stage,
    string? NextStage, long Revision, ProgramPlan Plan, Uuid LastChangedByMemberId,
    string LastChangedByDisplay, DateTimeOffset LastChangedAt);

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
public sealed record GetProgram(Uuid TenantId, Uuid ProgramId)
    : IRequest<ProgramView>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.program.list", 1)]
public sealed record ListPrograms(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ProgramView>>, ITenantAccessRequest, ICallable;

[Discriminator("bdgrz.program.created", 1)]
public sealed record ProgramCreated(Uuid TenantId, Uuid ProgramId, string Name, ProgramPlan Plan,
    Uuid ActorMemberId, string ActorDisplay, DateTimeOffset ChangedAt) : DomainEvent;

[Discriminator("bdgrz.program.revised", 1)]
public sealed record ProgramRevised(Uuid TenantId, Uuid ProgramId, long Revision,
    string Name, ProgramPlan Plan, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset ChangedAt) : DomainEvent;
