using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class RecordApplicationRelationshipHandler(IAggregateExecutor executor,
    IAggregateReader reader, RestrictedApplicationVisibility visibility,
    TimeProvider clock)
    : IRequestHandler<RecordApplicationRelationship, ApplicationRelationshipRegistration>
{
    public async ValueTask<Result<ApplicationRelationshipRegistration>> HandleAsync(
        IRequestContext<RecordApplicationRelationship> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.SourceApplicationId == Uuid.Empty ||
            request.TargetApplicationId == Uuid.Empty ||
            request.SourceApplicationId == request.TargetApplicationId ||
            !ApplicationRelationshipIdentity.IsSupported(request.RelationshipType))
            return Result<ApplicationRelationshipRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation, "The application relationship is invalid."));
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result<ApplicationRelationshipRegistration>.Failure(new RequestError(
                RequestErrorKind.Unauthorized, "A personal member identity is required."));

        var source = await reader.HydrateApplicationAsync(request.TenantId,
            request.SourceApplicationId, ct).ConfigureAwait(false);
        var target = await reader.HydrateApplicationAsync(request.TenantId,
            request.TargetApplicationId, ct).ConfigureAwait(false);
        if (!source.IsCreated || !target.IsCreated ||
            !await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.SourceApplicationId, ct).ConfigureAwait(false) ||
            !await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.TargetApplicationId, ct).ConfigureAwait(false))
            return Result<ApplicationRelationshipRegistration>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."));
        if (source.IsRetired || target.IsRetired)
            return Result<ApplicationRelationshipRegistration>.Failure(new RequestError(
                RequestErrorKind.Conflict, "Relationships require active applications."));

        var targetAggregate = await ApplicationImportWriteGuard.PrepareAsync(reader,
            request.TenantId, request.SourceApplicationId, ct).ConfigureAwait(false);
        var currentSource = await reader.HydrateApplicationAsync(request.TenantId,
            request.SourceApplicationId, ct).ConfigureAwait(false);
        var currentTarget = await reader.HydrateApplicationAsync(request.TenantId,
            request.TargetApplicationId, ct).ConfigureAwait(false);
        if (!currentSource.IsCreated || !currentTarget.IsCreated ||
            currentSource.IsRetired || currentTarget.IsRetired)
            return Result<ApplicationRelationshipRegistration>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application relationship endpoints changed."));
        if (currentSource.Revision != source.Revision || currentTarget.Revision != target.Revision)
            return Result<ApplicationRelationshipRegistration>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application relationship endpoints changed."));
        return await executor.ExecuteAsync(targetAggregate,
            app => app.CheckPendingImportChanges() is { } importError
                ? AggregateOutcome.Discard(Result<ApplicationRelationshipRegistration>.Failure(
                    importError))
                : AggregateOutcome.CommitOnSuccess(app.RecordRelationship(
                    request.TargetApplicationId, currentSource.Revision, currentTarget.Revision,
                    request.RelationshipType, RbacIds.Member(request.TenantId, userId),
                    UserIdentityClaims.BdgrzDisplay(context.Actor, userId),
                    clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
