using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

public sealed class GetApplicationHandler(IApplicationDirectoryReader directory,
    IAggregateReader reader, RestrictedApplicationVisibility visibility)
    : IRequestHandler<GetApplication, ApplicationView>
{
    public async ValueTask<Result<ApplicationView>> HandleAsync(
        IRequestContext<GetApplication> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.MinimumRevision is < 1)
            return Result<ApplicationView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum application revision must be positive."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (!await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.ApplicationId, ct).ConfigureAwait(false))
            return Result<ApplicationView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."));
        var view = await directory.GetAsync(request.TenantId, request.ApplicationId, ct)
            .ConfigureAwait(false);
        if (view is not null && (view.TenantId != request.TenantId ||
                                 view.ApplicationId != request.ApplicationId))
            return Result<ApplicationView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."));
        if (request.MinimumRevision is { } minimum && (view is null || view.Revision < minimum))
        {
            var source = await reader.HydrateApplicationAsync(request.TenantId, request.ApplicationId, ct).ConfigureAwait(false);
            if (!source.IsCreated)
                return Result<ApplicationView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The application was not found."));
            return Result<ApplicationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                source.Revision < minimum
                    ? $"The application source has not reached revision {minimum}."
                    : $"The application projection has not reached revision {minimum}."));
        }
        return view is null
            ? Result<ApplicationView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."))
            : Result<ApplicationView>.Success(view);
    }
}
