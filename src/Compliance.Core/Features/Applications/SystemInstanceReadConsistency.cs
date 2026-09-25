using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class SystemInstanceReadConsistency(IApplicationDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid applicationId,
        long? minimumApplicationRevision, Uuid? systemInstanceId, CancellationToken ct)
    {
        if (minimumApplicationRevision is < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum application revision must be positive."));
        var source = await reader.HydrateAsync(new DeclaredApplication(tenantId, applicationId),
            ct).ConfigureAwait(false);
        if (!source.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."));
        if (systemInstanceId is { } instanceId && !source.HasInstance(instanceId))
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The system instance was not found."));
        if (minimumApplicationRevision is { } minimum && source.Revision < minimum)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The application source has not reached revision {minimum}.",
                isTransient: true));
        var projection = await directory.GetAsync(tenantId, applicationId, ct)
            .ConfigureAwait(false);
        if (projection is null || projection.TenantId != tenantId ||
            projection.ApplicationId != applicationId || projection.Revision < source.Revision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The application projection has not reached the source revision.",
                isTransient: true));
        return Result.Success;
    }
}
