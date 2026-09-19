using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class CreateProgramHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<CreateProgram, ProgramRegistration>
{
    public ValueTask<Result<ProgramRegistration>> HandleAsync(IRequestContext<CreateProgram> context,
        CancellationToken ct)
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return executor.ExecuteAsync(new ComplianceProgram(context.Request.TenantId, context.RequestId),
            program => AggregateOutcome.CommitOnSuccess(program.Create(context.Request.Name,
                context.Request.Plan, RbacIds.Member(context.Request.TenantId, userId),
                context.Actor.FindFirst("email")?.Value ?? userId.ToString(), clock.GetUtcNow())),
            context, ct);
    }
}
