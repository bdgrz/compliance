using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ReviseApplicationHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviseApplication>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseApplication> context,
        CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        return executor.ExecuteAsync(new DeclaredApplication(request.TenantId, request.ApplicationId),
            app => AggregateOutcome.CommitOnSuccess(app.Revise(request.ExpectedRevision,
                request.Name, request.Purpose, request.OwnerReference,
                memberId, display, clock.GetUtcNow(), request.Classification)), context, ct);
    }
}
