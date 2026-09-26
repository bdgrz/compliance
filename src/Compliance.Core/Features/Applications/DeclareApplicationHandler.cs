using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class DeclareApplicationHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock)
    : IRequestHandler<DeclareApplication, ApplicationRegistration>
{
    public async ValueTask<Result<ApplicationRegistration>> HandleAsync(
        IRequestContext<DeclareApplication> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        var owners = await ApplicationOwnerReferences.ValidateAsync(reader, request.TenantId,
            request.SystemOwnerPersonId, request.AccessOwnerPersonId, ct).ConfigureAwait(false);
        if (owners is not null)
            return Result<ApplicationRegistration>.Failure(owners);
        return await executor.ExecuteAsync(new DeclaredApplication(request.TenantId,
                context.RequestId),
            app => AggregateOutcome.CommitOnSuccess(app.Declare(request.Name, request.Purpose,
                request.OwnerReference, memberId, display, clock.GetUtcNow(),
                request.Classification, request.SystemOwnerPersonId,
                request.AccessOwnerPersonId)), context, ct).ConfigureAwait(false);
    }
}
