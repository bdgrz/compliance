using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>One operating responsibility the acting member holds directly or through a team.</summary>
public sealed record ControlResponsibilityView(Uuid ControlId, Uuid PlanVersionId, string Role,
    OperatingHolder Holder, DateOnly EffectiveFrom, DateOnly? EffectiveUntil,
    string CadenceDescription);
