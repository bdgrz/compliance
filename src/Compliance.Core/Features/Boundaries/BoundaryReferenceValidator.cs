using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public interface IBoundaryReferenceValidator
{
    ValueTask<Result> ValidateAsync(Uuid tenantId, Uuid programId, BoundaryContent content,
        CancellationToken ct = default);
}

public sealed class GovernedBoundaryReferenceValidator(IClientServiceActivity services,
    IApplicationInventoryActivity applications)
    : IBoundaryReferenceValidator
{
    public async ValueTask<Result> ValidateAsync(Uuid tenantId, Uuid programId, BoundaryContent content,
        CancellationToken ct = default)
    {
        if (content?.Entries is null)
            return Result.Success;
        foreach (var entry in content.Entries.Where(static entry => entry is { Unresolved: false }))
        {
            if (entry.GovernedRecordId is not { } id)
                return Result.Failure(new RequestError(RequestErrorKind.Validation,
                    "A governed scope reference requires a record ID."));
            switch (entry.SubjectType)
            {
                case "service":
                    if (!await services.IsActiveAsync(tenantId, programId, id, ct)
                            .ConfigureAwait(false))
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The governed service reference is not active in this program."));
                    break;
                case "application":
                    if (!await applications.IsDeclaredAsync(tenantId, id, ct)
                            .ConfigureAwait(false))
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The governed application reference is unavailable in this tenant."));
                    break;
                case "system_instance":
                    var state = await applications.GetInstanceStateAsync(tenantId, id, ct)
                        .ConfigureAwait(false);
                    if (state == SystemInstanceReferenceState.Pending)
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The governed system instance reference has not finished projecting. Retry the command.",
                            isTransient: true));
                    if (state != SystemInstanceReferenceState.Declared)
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The governed system instance reference is unavailable in this tenant. Retry after projection or correct the record ID."));
                    break;
                default:
                    return Result.Failure(new RequestError(RequestErrorKind.Validation,
                        "A governed scope reference requires its owning inventory to validate the record."));
            }
        }
        return Result.Success;
    }
}
