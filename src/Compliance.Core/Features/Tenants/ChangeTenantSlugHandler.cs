using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ChangeTenantSlugHandler(IAggregateReader reader, IAggregateExecutor executor,
    IPlatformOperatorAccess operators, TimeProvider clock)
    : IRequestHandler<ChangeTenantSlug>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ChangeTenantSlug> context, CancellationToken ct)
    {
        if (!TenantSlugs.TryNormalize(context.Request.Slug, out var slug, out var reason))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, reason));
        var candidate = await reader.HydrateAsync(new TenantSlug(slug), ct).ConfigureAwait(false);
        if (!candidate.IsAvailableFor(context.Request.TenantId))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The tenant slug is already reserved or retired."));

        ActorReference? requestedBy = null;
        if (UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
        {
            var display = UserIdentityClaims.BdgrzDisplay(context.Actor, userId);
            requestedBy = await operators.IsOperatorAsync(userId, ct).ConfigureAwait(false)
                ? ActorReference.ForPlatformOperator(userId, display)
                : ActorReference.ForMember(RbacIds.Member(context.Request.TenantId, userId), display);
        }

        return await executor.ExecuteAsync(new Tenant(context.Request.TenantId),
            tenant => AggregateOutcome.CommitOnSuccess(tenant.RequestSlugChange(slug, requestedBy,
                clock.GetUtcNow())),
            context, ct).ConfigureAwait(false);
    }
}
