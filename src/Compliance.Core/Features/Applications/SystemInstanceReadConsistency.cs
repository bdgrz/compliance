using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class SystemInstanceReadConsistency(IApplicationDirectoryReader directory,
    IAggregateReader reader, LegacySystemInstanceSource legacy,
    IDomainEventReader events)
{
    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid applicationId,
        long? minimumApplicationRevision, Uuid? systemInstanceId,
        long? minimumInstanceRevision, CancellationToken ct)
    {
        if (minimumApplicationRevision is < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum application revision must be positive."));
        if (minimumInstanceRevision is < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum system instance revision must be positive."));
        var source = await reader.HydrateAsync(new DeclaredApplication(tenantId, applicationId),
            ct).ConfigureAwait(false);
        if (!source.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."));
        if (minimumApplicationRevision is { } minimum && source.Revision < minimum)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The application source has not reached revision {minimum}.",
                isTransient: true));
        var projection = await directory.GetAsync(tenantId, applicationId, ct)
            .ConfigureAwait(false);
        if (projection is null || projection.Revision < source.Revision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The application projection has not reached the source revision.",
                isTransient: true));
        if (systemInstanceId is { } instanceId)
        {
            var instance = await reader.HydrateAsync(new DeclaredSystemInstance(tenantId,
                instanceId), ct).ConfigureAwait(false);
            long instanceRevision;
            if (instance.IsCreated)
            {
                if (instance.ApplicationId != applicationId)
                    return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                        "The system instance was not found."));
                instanceRevision = instance.Revision;
            }
            else
            {
                if (!await legacy.ExistsAsync(tenantId, applicationId, instanceId, ct)
                        .ConfigureAwait(false))
                    return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                        "The system instance was not found."));
                instanceRevision = 1;
            }
            if (minimumInstanceRevision is { } minimumInstance &&
                instanceRevision < minimumInstance)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    $"The system instance source has not reached revision {minimumInstance}.",
                    isTransient: true));
            var projected = await directory.GetInstanceAsync(tenantId, instanceId, ct)
                .ConfigureAwait(false);
            if (projected is null || projected.Revision < instanceRevision)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The system instance projection has not reached the source revision.",
                    isTransient: true));
            if (projected.ApplicationId != applicationId)
                return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The system instance was not found."));
        }
        else
        {
            // A list has no single instance stream to inspect. The versioned projector
            // processes old application and new instance events in one tenant cursor.
            var backlog = await ApplicationDirectoryBacklog.FindAsync(directory, events,
                tenantId, static ev => ev is ApplicationDeclared or ApplicationRevised or
                    SystemInstanceDeclared or SystemInstanceRegistered, ct).ConfigureAwait(false);
            if (!backlog.Clear)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The system instance list projection has not reached the source.",
                    isTransient: true));
        }
        return Result.Success;
    }
}
