using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public interface IBoundaryReferenceValidator
{
    ValueTask<Result> ValidateAsync(Uuid tenantId, BoundaryContent content,
        CancellationToken ct = default);
}

public sealed class GovernedBoundaryReferenceValidator(IClientServiceDirectoryReader services)
    : IBoundaryReferenceValidator
{
    public async ValueTask<Result> ValidateAsync(Uuid tenantId, BoundaryContent content,
        CancellationToken ct = default)
    {
        if (content?.Entries is null)
            return Result.Success;
        foreach (var entry in content.Entries.Where(static entry => entry is { Unresolved: false }))
        {
            if (entry.SubjectType != "service")
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "A governed scope reference requires its owning inventory to validate the record."));
            var service = entry.GovernedRecordId is { } id
                ? await services.GetAsync(tenantId, id, ct).ConfigureAwait(false)
                : null;
            if (service is null)
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The governed service reference is not available in this tenant. Retry after projection catches up."));
            if (service.Status != "active")
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "The governed service reference is not active."));
        }
        return Result.Success;
    }
}
