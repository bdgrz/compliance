using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Change record, impact assessment, and linked review work.</summary>
public sealed record SignificantChangeView(Uuid TenantId, Uuid ProgramId, Uuid SignificantChangeId, string Title, string Status, DateOnly EffectiveOn, string? ImpactStatus);
