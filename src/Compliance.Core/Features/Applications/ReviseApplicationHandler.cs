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
        return await executor.ExecuteAsync(new DeclaredApplication(request.TenantId,
                request.ApplicationId),
            app => AggregateOutcome.CommitOnSuccess(app.Revise(request.ExpectedRevision,
                request.Name, request.Purpose, request.OwnerReference,
                memberId, display, clock.GetUtcNow(), request.Classification,
                request.SystemOwnerPersonId, request.AccessOwnerPersonId)), context, ct)
            .ConfigureAwait(false);
    }
}
