using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Boundaries;

public interface IBoundaryReferenceValidator
{
    ValueTask<Result> ValidateAsync(Uuid tenantId, Uuid programId, BoundaryContent content,
        CancellationToken ct = default);
}
