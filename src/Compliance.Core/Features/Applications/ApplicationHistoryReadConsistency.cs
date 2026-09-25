using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class ApplicationHistoryReadConsistency(IApplicationDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid applicationId,
        long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum application revision must be positive."));
        var view = await directory.GetAsync(tenantId, applicationId, ct)
            .ConfigureAwait(false);
        if (view is not null && view.TenantId == tenantId &&
            view.ApplicationId == applicationId &&
            (minimumRevision is null || view.Revision >= minimumRevision))
            return Result.Success;
        var source = await reader.HydrateAsync(new DeclaredApplication(tenantId,
            applicationId), ct).ConfigureAwait(false);
        if (!source.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."));
        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
            minimumRevision is { } minimum && source.Revision < minimum
                ? $"The application source has not reached revision {minimum}."
                : "The application projection has not reached the requested revision."));
    }
}
