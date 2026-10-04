using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class RegisterMemberHandler(IAggregateExecutor executor, IAggregateReader reader,
    MemberAuthorityCleanup cleanup) : IRequestHandler<RegisterMember>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<RegisterMember> context, CancellationToken ct)
    {
        var request = context.Request;
        var current = await reader.HydrateAsync(new Member(request.TenantId, request.UserId), ct)
            .ConfigureAwait(false);
        if (current.IsDeprovisioned && !current.IsDeprovisionCleanupComplete)
        {
            var cleanupResult = await cleanup.CompleteAsync(context, request.TenantId, request.UserId,
                current.Id, current.DeprovisionedByMemberId, current.DeprovisionedByDisplay!, ct)
                .ConfigureAwait(false);
            if (!cleanupResult.IsSuccess)
                return cleanupResult;
        }

        return await executor.ExecuteAsync(new Member(request.TenantId, request.UserId),
            member => AggregateOutcome.CommitOnSuccess(member.Register(request.Affiliation)), context, ct)
            .ConfigureAwait(false);
    }
}
