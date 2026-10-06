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
        var target = await ApplicationImportWriteGuard.PrepareAsync(reader, request.TenantId,
            context.RequestId, ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(target,
            app => AggregateOutcome.CommitOnSuccess(app.Declare(request.Name, request.Purpose,
                request.OwnerReference, memberId, display, clock.GetUtcNow(),
                request.Classification, request.SystemOwnerPersonId,
                request.AccessOwnerPersonId, request.IsRestricted)), context, ct).ConfigureAwait(false);
    }
}
