using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class DeclareApplicationHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock, ApplicationGovernedDeclarationGate? gate = null)
    : IRequestHandler<DeclareApplication, ApplicationRegistration>
{
    public async ValueTask<Result<ApplicationRegistration>> HandleAsync(
        IRequestContext<DeclareApplication> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        if (request.SystemOwnerPersonId == Uuid.Empty || request.AccessOwnerPersonId == Uuid.Empty)
            return Result<ApplicationRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "Application owners must identify recorded workforce people in this tenant."));
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
                request.AccessOwnerPersonId, request.IsRestricted, request.ApplicationKind,
                request.Criticality, gate?.IsEnabled ?? false)), context, ct).ConfigureAwait(false);
    }
}
