using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class RemoveApplicationRelationshipHandler(IAggregateExecutor executor,
    IAggregateReader reader, RestrictedApplicationVisibility visibility,
    TimeProvider clock) : IRequestHandler<RemoveApplicationRelationship>
{
    public async ValueTask<Result> HandleAsync(
        IRequestContext<RemoveApplicationRelationship> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.SourceApplicationId == Uuid.Empty ||
            request.TargetApplicationId == Uuid.Empty ||
            request.SourceApplicationId == request.TargetApplicationId ||
            !ApplicationRelationshipIdentity.IsSupported(request.RelationshipType))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The application relationship removal is invalid."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "A personal member identity is required."));

        var source = await reader.HydrateApplicationAsync(request.TenantId,
            request.SourceApplicationId, ct).ConfigureAwait(false);
        var target = await reader.HydrateApplicationAsync(request.TenantId,
            request.TargetApplicationId, ct).ConfigureAwait(false);
        if (!source.IsCreated || !target.IsCreated ||
            !await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.SourceApplicationId, ct).ConfigureAwait(false) ||
            !await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.TargetApplicationId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."));
        if (source.IsRetired || target.IsRetired)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Relationships require active applications."));

        var targetAggregate = await ApplicationImportWriteGuard.PrepareAsync(reader,
            request.TenantId, request.SourceApplicationId, ct).ConfigureAwait(false);
        var currentSource = await reader.HydrateApplicationAsync(request.TenantId,
            request.SourceApplicationId, ct).ConfigureAwait(false);
        var currentTarget = await reader.HydrateApplicationAsync(request.TenantId,
            request.TargetApplicationId, ct).ConfigureAwait(false);
        if (!currentSource.IsCreated || !currentTarget.IsCreated ||
            currentSource.IsRetired || currentTarget.IsRetired ||
            !await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.SourceApplicationId, ct).ConfigureAwait(false) ||
            !await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.TargetApplicationId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."));
        return await executor.ExecuteAsync(targetAggregate,
            app => app.CheckPendingImportChanges() is { } importError
                ? AggregateOutcome.Discard(Result.Failure(importError))
                : CommandFailureRequestAdapter.ToOutcome(app.RemoveRelationship(
                    request.TargetApplicationId, request.ExpectedRelationshipRevision,
                    request.RelationshipType, request.Reason, RbacIds.Member(request.TenantId,
                        userId), UserIdentityClaims.BdgrzDisplay(context.Actor, userId),
                    clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
