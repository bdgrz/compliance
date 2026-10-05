using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record ControlOccurrencePlanWorkState(Uuid TenantId, Uuid ProgramId, Uuid ControlId,
    Uuid PlanVersionId, Uuid ControlVersionId, long Revision, string Status,
    OperatingHolder Owner, OperatingHolder? BackupOwner, Uuid ReviewerMemberId,
    ControlCadence Cadence, DateOnly EffectiveFrom, DateOnly? EffectiveUntil);
