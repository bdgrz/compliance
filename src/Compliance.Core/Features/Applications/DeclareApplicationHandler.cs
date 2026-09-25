using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class DeclareApplicationHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<DeclareApplication, ApplicationRegistration>
{
    public ValueTask<Result<ApplicationRegistration>> HandleAsync(
        IRequestContext<DeclareApplication> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        return executor.ExecuteAsync(new DeclaredApplication(request.TenantId, context.RequestId),
            app => AggregateOutcome.CommitOnSuccess(app.Declare(request.Name, request.Purpose,
                request.OwnerReference, memberId, display, clock.GetUtcNow(),
                request.Classification)), context, ct);
    }
}
