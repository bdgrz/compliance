using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class GetClientServiceHandler(IClientServiceDirectoryReader directory,
    IAggregateReader reader)
    : IRequestHandler<GetClientService, ClientServiceView>
{
    public async ValueTask<Result<ClientServiceView>> HandleAsync(IRequestContext<GetClientService> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.MinimumRevision is < 1)
            return Result<ClientServiceView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum revision must be positive."));
        var service = await directory.GetAsync(request.TenantId,
            request.ServiceId, ct).ConfigureAwait(false);
        if (request.MinimumRevision is { } minimum &&
            (service is null || service.Revision < minimum))
        {
            var current = await reader.HydrateAsync(new ClientService(request.TenantId,
                request.ServiceId), ct).ConfigureAwait(false);
            if (!current.IsCreated)
                return Result<ClientServiceView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The service was not found."));
            return Result<ClientServiceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                current.Revision < minimum
                    ? $"The service source has not reached revision {minimum}."
                    : $"The service projection has not reached revision {minimum}."));
        }
        return service is null
            ? Result<ClientServiceView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The service was not found."))
            : Result<ClientServiceView>.Success(service);
    }
}
