using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

public interface IControlApplicabilityReferenceValidator
{
    ValueTask<Result> ValidateAsync(Uuid tenantId, ControlDraftContent? content,
        CancellationToken ct = default);
}
