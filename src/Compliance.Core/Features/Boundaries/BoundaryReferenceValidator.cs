using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public interface IBoundaryReferenceValidator
{
    ValueTask<Result> ValidateAsync(Uuid tenantId, BoundaryContent content,
        CancellationToken ct = default);
}

// Governed inventory readers replace this validator as their owning features arrive.
// Until then, callers may preserve an explicit unresolved reference but cannot claim
// that an arbitrary UUID identifies an approved inventory record.
public sealed class PendingInventoryBoundaryReferenceValidator : IBoundaryReferenceValidator
{
    public ValueTask<Result> ValidateAsync(Uuid tenantId, BoundaryContent content,
        CancellationToken ct = default) =>
        ValueTask.FromResult(content?.Entries is not null && content.Entries.Any(
            static entry => entry is { Unresolved: false })
            ? Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A governed scope reference requires its owning inventory to validate the record."))
            : Result.Success);
}
