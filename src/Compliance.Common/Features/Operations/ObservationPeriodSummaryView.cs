using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Program period history and current status.</summary>
public sealed record ObservationPeriodSummaryView(Uuid TenantId, Uuid ProgramId, Uuid ObservationPeriodId, string Name, string Status, DateTimeOffset UpdatedAt);
