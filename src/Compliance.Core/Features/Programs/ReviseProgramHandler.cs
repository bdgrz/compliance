using Bdgrz.Compliance.Features.Versioning;
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
            program => CommandFailureRequestAdapter.ToOutcome(
                program.Revise(context.Request.ExpectedRevision,
                    context.Request.Name, context.Request.Plan,
                    RbacIds.Member(context.Request.TenantId, userId),
                    UserIdentityClaims.BdgrzDisplay(context.Actor, userId), clock.GetUtcNow())),
            context, ct);
    }
}
