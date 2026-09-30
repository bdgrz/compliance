using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class ReviseServiceIdentityHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<ReviseServiceIdentity>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseServiceIdentity> context,
        CancellationToken ct)
    {
        var actor = WorkforceActor.From(context);
        var request = context.Request;
        var current = await reader.HydrateAsync(
            new ServiceIdentity(request.TenantId, request.ServiceIdentityId), ct)
            .ConfigureAwait(false);
        if (current.IsCreated && request.LifecycleStatus != "retired")
        {
            var invalid = await ServiceIdentityOwners.ValidateAsync(reader, request.TenantId,
                request.OwnerKind, request.OwnerId, ct).ConfigureAwait(false);
            if (invalid is not null)
                return Result.Failure(invalid);
        }
        var now = clock.GetUtcNow();
        var terms = new ServiceIdentityTerms(request.DisplayName, request.IdentityKind,
            request.Purpose, request.Environment, request.LifecycleStatus, request.OwnerKind,
            request.OwnerId, request.ReviewBy);
        return await executor.ExecuteAsync(
            new ServiceIdentity(request.TenantId, request.ServiceIdentityId),
            identity => CommandFailureRequestAdapter.ToOutcome(identity.Revise(
                request.ExpectedRevision, terms, DateOnly.FromDateTime(now.UtcDateTime), actor,
                now)), context, ct).ConfigureAwait(false);
    }
}
