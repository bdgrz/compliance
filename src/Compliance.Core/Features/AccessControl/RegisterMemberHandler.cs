using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RegisterMemberHandler(IAggregateExecutor executor) : IRequestHandler<RegisterMember>
{
    public ValueTask<Result> HandleAsync(IRequestContext<RegisterMember> context, CancellationToken ct) =>
        executor.ExecuteAsync(
            new Member(context.Request.TenantId, context.Request.UserId),
            member => AggregateOutcome.CommitOnSuccess(member.Register()),
            context, ct);
}
