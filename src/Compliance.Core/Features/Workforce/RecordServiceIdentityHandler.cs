using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class RecordServiceIdentityHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<RecordServiceIdentity, ServiceIdentityRegistration>
{
    public async ValueTask<Result<ServiceIdentityRegistration>> HandleAsync(
        IRequestContext<RecordServiceIdentity> context, CancellationToken ct)
    {
        var actor = WorkforceActor.From(context);
        var request = context.Request;
        var invalid = await ServiceIdentityOwners.ValidateAsync(reader, request.TenantId,
            request.OwnerKind, request.OwnerId, ct).ConfigureAwait(false);
        if (invalid is not null)
            return Result<ServiceIdentityRegistration>.Failure(invalid);
        var now = clock.GetUtcNow();
        var terms = new ServiceIdentityTerms(request.DisplayName, request.IdentityKind,
            request.Purpose, request.Environment, "active", request.OwnerKind, request.OwnerId,
            request.ReviewBy, request.ExpiresOn);
        return await executor.ExecuteAsync(new ServiceIdentity(request.TenantId, context.RequestId),
            identity => AggregateOutcome.CommitOnSuccess(identity.Record(terms,
                DateOnly.FromDateTime(now.UtcDateTime), actor, now)), context, ct)
            .ConfigureAwait(false);
    }
}
