using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public interface IBoundaryReferenceValidator
{
    ValueTask<Result> ValidateAsync(Uuid tenantId, BoundaryContent content,
        CancellationToken ct = default);
}

public sealed class GovernedBoundaryReferenceValidator(IClientServiceActivity services)
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
            if (entry.GovernedRecordId is not { } id)
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "A governed service reference requires a service ID."));
            if (!await services.IsActiveAsync(tenantId, id, ct).ConfigureAwait(false))
                return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The governed service reference is not active in this tenant."));
        }
        return Result.Success;
    }
}
