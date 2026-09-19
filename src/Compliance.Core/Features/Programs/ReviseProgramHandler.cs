using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ReviseProgramHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviseProgram>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseProgram> context, CancellationToken ct)
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ProgramManagementAuthorizer must reject this actor.");
        return executor.ExecuteAsync(
            new ComplianceProgram(context.Request.TenantId, context.Request.ProgramId),
            program => AggregateOutcome.CommitOnSuccess(program.Revise(context.Request.ExpectedRevision,
                context.Request.Name, context.Request.Plan,
                RbacIds.Member(context.Request.TenantId, userId),
                context.Actor.FindFirst("email")?.Value ?? userId.ToString(), clock.GetUtcNow())),
            context, ct);
    }
}
