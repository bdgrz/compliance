using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ReviseApplicationHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock) : IRequestHandler<ReviseApplication>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseApplication> context,
        CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        var owners = await ApplicationOwnerReferences.ValidateAsync(reader, request.TenantId,
            request.SystemOwnerPersonId, request.AccessOwnerPersonId, ct).ConfigureAwait(false);
        if (owners is not null)
            return Result.Failure(owners);
        var target = await ApplicationImportWriteGuard.PrepareAsync(reader, request.TenantId, request.ApplicationId, ct)
            .ConfigureAwait(false);
        return await executor.ExecuteAsync(target,
            app => app.CheckPendingImportChanges() is { } importError
                ? AggregateOutcome.Discard(Result.Failure(importError))
                : CommandFailureRequestAdapter.ToOutcome(app.Revise(request.ExpectedRevision,
                request.Name, request.Purpose, request.OwnerReference,
                memberId, display, clock.GetUtcNow(), request.Classification,
                request.SystemOwnerPersonId, request.AccessOwnerPersonId,
                request.IsRestricted)), context, ct)
            .ConfigureAwait(false);
    }
}
