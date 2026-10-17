using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Changes within the authorized program/period scope.</summary>
public sealed record SignificantChangeSummaryView(Uuid TenantId, Uuid ProgramId, Uuid SignificantChangeId, string Name, string Status, DateTimeOffset UpdatedAt);
