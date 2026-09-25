using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public sealed class GovernedControlApplicabilityReferenceValidator(
    IApplicationInventoryActivity applications)
    : IControlApplicabilityReferenceValidator
{
    public async ValueTask<Result> ValidateAsync(Uuid tenantId, ControlDraftContent? content,
        CancellationToken ct = default)
    {
        foreach (var reference in content?.Applicability ?? [])
        {
            if (reference is null || reference.Unresolved ||
                reference.GovernedRecordId is null || reference.GovernedRecordId == Uuid.Empty)
                continue;
            var recordId = reference.GovernedRecordId.Value;
            switch (reference.SubjectType)
            {
                case "application":
                    if (!await applications.IsDeclaredAsync(tenantId, recordId, ct)
                            .ConfigureAwait(false))
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The governed control application reference is unavailable in this tenant."));
                    break;
                case "system_instance":
                    if (!await applications.IsInstanceDeclaredAsync(tenantId, recordId, ct)
                            .ConfigureAwait(false))
                        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                            "The governed control system instance reference is unavailable in this tenant. Retry after projection or correct the record ID."));
                    break;
                default:
                    return Result.Failure(new RequestError(RequestErrorKind.Validation,
                        "A governed control applicability reference requires its owning inventory to validate the record."));
            }
        }
        return Result.Success;
    }
}
