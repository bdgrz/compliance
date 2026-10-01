using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Previews a proposed operating plan without recording it.</summary>
[Discriminator("bdgrz.control.operating_plan.preview", 1)]
public sealed record PreviewControlOperatingPlan(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid ControlVersionId, OperatingHolder Owner, OperatingHolder? BackupOwner,
    Uuid ReviewerMemberId, ControlCadence Cadence, DateOnly EffectiveFrom)
    : IRequest<ControlOperatingPlanPreview>, IProgramScopedRequest, ICallable;
