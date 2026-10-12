using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ReviseApplicationHandler(IAggregateExecutor executor,
    IAggregateReader reader, TimeProvider clock, ApplicationGovernedRevisionGate? gate = null)
    : IRequestHandler<ReviseApplication>
{
    public async ValueTask<Result> HandleAsync(IRequestContext<ReviseApplication> context,
        CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        var governed = gate?.IsEnabled ?? false;
        ApplicationRevisionContent? validated = null;
        var systemOwner = request.SystemOwnerPersonId;
        var accessOwner = request.AccessOwnerPersonId;
        if (governed)
        {
            var observed = await reader.HydrateAsync(await ApplicationImportWriteGuard.PrepareAsync(
                reader, request.TenantId, request.ApplicationId, ct).ConfigureAwait(false), ct)
                .ConfigureAwait(false);
            if (observed.CheckPendingImportChanges() is { } pendingImport)
                return Result.Failure(pendingImport);
            var invalid = Resolve(observed, request, governed, out var content);
            if (invalid is not null)
                return Result.Failure(invalid.ToRequestError());
            validated = content;
            systemOwner = content.SystemOwnerPersonId;
            accessOwner = content.AccessOwnerPersonId;
        }
        var owners = await ApplicationOwnerReferences.ValidateAsync(reader, request.TenantId,
            systemOwner, accessOwner, ct).ConfigureAwait(false);
        if (owners is not null)
            return Result.Failure(owners);
        var target = await ApplicationImportWriteGuard.PrepareAsync(reader, request.TenantId, request.ApplicationId, ct)
            .ConfigureAwait(false);
        return await executor.ExecuteAsync(target,
            app =>
            {
                if (app.CheckPendingImportChanges() is { } importError)
                    return AggregateOutcome.Discard(Result.Failure(importError));
                if (governed)
                {
                    var changed = Resolve(app, request, governed, out var current);
                    if (changed is not null)
                        return CommandFailureRequestAdapter.ToOutcome(changed);
                    if (current != validated)
                        return AggregateOutcome.Discard(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The application content changed after owner validation.")));
                }
                return CommandFailureRequestAdapter.ToOutcome(app.Revise(request.ExpectedRevision,
                request.Name, request.Purpose, request.OwnerReference,
                memberId, display, clock.GetUtcNow(), request.Classification,
                request.SystemOwnerPersonId, request.AccessOwnerPersonId,
                request.IsRestricted, request.ApplicationKind, request.Criticality, governed,
                request.ClassificationIntent, request.SystemOwnerIntent, request.AccessOwnerIntent));
            }, context, ct)
            .ConfigureAwait(false);
    }

    static CommandFailure? Resolve(DeclaredApplication app, ReviseApplication request,
        bool governed, out ApplicationRevisionContent content) =>
        app.ResolveRevisionContent(request.ExpectedRevision, request.Name, request.Purpose,
            request.OwnerReference, request.Classification, request.SystemOwnerPersonId,
            request.AccessOwnerPersonId, request.IsRestricted, request.ApplicationKind,
            request.Criticality, governed, request.ClassificationIntent,
            request.SystemOwnerIntent, request.AccessOwnerIntent, out content);
}
