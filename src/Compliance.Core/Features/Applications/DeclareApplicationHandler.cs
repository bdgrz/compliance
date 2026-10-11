using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class DeclareApplicationHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock, RestrictedApplicationVisibility visibility,
    ApplicationGovernedDeclarationGate? gate = null)
    : IRequestHandler<DeclareApplication, ApplicationRegistration>
{
    public async ValueTask<Result<ApplicationRegistration>> HandleAsync(
        IRequestContext<DeclareApplication> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        if (request.DeclarationKey == Uuid.Empty)
            return Result<ApplicationRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "An application declaration key cannot be empty."));
        if (request.SystemOwnerPersonId == Uuid.Empty || request.AccessOwnerPersonId == Uuid.Empty)
            return Result<ApplicationRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "Application owners must identify recorded workforce people in this tenant."));
        var owners = await ApplicationOwnerReferences.ValidateAsync(reader, request.TenantId,
            request.SystemOwnerPersonId, request.AccessOwnerPersonId, ct).ConfigureAwait(false);
        if (owners is not null)
            return Result<ApplicationRegistration>.Failure(owners);
        var applicationId = request.DeclarationKey is { } declarationKey
            ? Uuid.CreateVersion5(request.TenantId,
                $"application_declaration_v1:{memberId}:{declarationKey}")
            : context.RequestId;
        var canReadRestricted = request.DeclarationKey is not null &&
            UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) &&
            await visibility.HasRestrictedApplicationPermissionAsync(request.TenantId, userId,
                applicationId, ct).ConfigureAwait(false);
        var target = await ApplicationImportWriteGuard.PrepareAsync(reader, request.TenantId,
            applicationId, ct).ConfigureAwait(false);
        return await executor.ExecuteAsync(target,
            app =>
            {
                if (request.DeclarationKey is not null && app.IsCreated && app.IsRestricted &&
                    !canReadRestricted)
                    return AggregateOutcome.Discard(Result<ApplicationRegistration>.Failure(
                        new RequestError(RequestErrorKind.NotFound, "The application was not found.")));
                return AggregateOutcome.CommitOnSuccess(app.Declare(request.Name, request.Purpose,
                    request.OwnerReference, memberId, display, clock.GetUtcNow(),
                    request.Classification, request.SystemOwnerPersonId,
                    request.AccessOwnerPersonId, request.IsRestricted, request.ApplicationKind,
                    request.Criticality, gate?.IsEnabled ?? false));
            }, context, ct).ConfigureAwait(false);
    }
}
