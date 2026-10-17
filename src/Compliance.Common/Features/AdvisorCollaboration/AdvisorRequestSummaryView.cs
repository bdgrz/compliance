using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Scoped handoff directory with status and review readiness.</summary>
public sealed record AdvisorRequestSummaryView(Uuid TenantId, Uuid ProgramId, Uuid AdvisorRequestId, string Name, string Status, DateTimeOffset UpdatedAt);
