using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class DeclareSystemInstanceHandler(IAggregateExecutor executor,
    IAggregateReader reader, IApplicationDirectoryReader directory,
    IDomainEventReader events, TimeProvider clock)
    : IRequestHandler<DeclareSystemInstance, SystemInstanceRegistration>
{
    public async ValueTask<Result<SystemInstanceRegistration>> HandleAsync(
        IRequestContext<DeclareSystemInstance> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        if (request.ExpectedApplicationRevision < 1)
            return Result<SystemInstanceRegistration>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The expected application revision must be positive."));
        var application = await reader.HydrateAsync(new DeclaredApplication(request.TenantId,
            request.ApplicationId), ct).ConfigureAwait(false);
        if (!application.IsCreated)
            return Result<SystemInstanceRegistration>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."));
        // V1 retains the field name. It is now a source freshness floor, not a lock on
        // unrelated application metadata edits.
        if (application.Revision < request.ExpectedApplicationRevision)
            return Result<SystemInstanceRegistration>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                $"The application source has not reached revision {request.ExpectedApplicationRevision}.",
                isTransient: true));
        // The V2 projector replays old and new streams in one tenant cursor. Until
        // pending old declarations are included, a projected ID lookup cannot rule
        // out a historical identity owned by another application.
        var backlog = await ApplicationDirectoryBacklog.FindAsync(directory, events,
            request.TenantId, static ev => ev is SystemInstanceDeclared, ct).ConfigureAwait(false);
        if (!backlog.Clear)
            return Result<SystemInstanceRegistration>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "Historical system instance declarations have not finished projecting.",
                isTransient: true));
        var existing = await directory.GetInstanceAsync(request.TenantId,
            context.RequestId, ct).ConfigureAwait(false);
        // A projected row may be a legacy declaration with no instance stream to replay.
        if (existing is not null)
            return DeclaredSystemInstance.IsSameDeclaration(existing.ApplicationId,
                    existing.Name, existing.Kind, existing.AccessBoundaryReference,
                    existing.SourceIdentifier, request.ApplicationId, request.Name,
                    request.Kind, request.AccessBoundaryReference, request.SourceIdentifier)
                ? Result<SystemInstanceRegistration>.Success(
                    new SystemInstanceRegistration(context.RequestId))
                : Result<SystemInstanceRegistration>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "The system instance already exists with different content."));
        return await executor.ExecuteAsync(new DeclaredSystemInstance(request.TenantId,
                context.RequestId),
            instance => AggregateOutcome.CommitOnSuccess(instance.Declare(
                request.ApplicationId, request.Name, request.Kind,
                request.AccessBoundaryReference, request.SourceIdentifier,
                memberId, display, clock.GetUtcNow())), context, ct).ConfigureAwait(false);
    }
}
