using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class DeclareSystemInstanceHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<DeclareSystemInstance, SystemInstanceRegistration>
{
    public ValueTask<Result<SystemInstanceRegistration>> HandleAsync(
        IRequestContext<DeclareSystemInstance> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        return executor.ExecuteAsync(new DeclaredApplication(request.TenantId, request.ApplicationId),
            app => AggregateOutcome.CommitOnSuccess(app.DeclareInstance(
                request.ExpectedApplicationRevision, context.RequestId,
                request.Name, request.Kind, request.AccessBoundaryReference,
                request.SourceIdentifier,
                memberId, display, clock.GetUtcNow())), context, ct);
    }
}
