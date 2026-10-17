using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Engagement-scoped requests and their current work status.</summary>
public sealed record AuditorRequestSummaryView(Uuid TenantId, Uuid ProgramId, Uuid AuditorRequestId, string Name, string Status, DateTimeOffset UpdatedAt);
