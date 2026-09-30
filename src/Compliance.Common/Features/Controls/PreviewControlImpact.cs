using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>Previews the pending successor or retirement at one exact control revision.</summary>
[Discriminator("bdgrz.control.impact.preview", 1)]
public sealed record PreviewControlImpact(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    long ExpectedRevision)
    : IRequest<ControlImpactPreview>, IProgramScopedRequest, ICallable;
