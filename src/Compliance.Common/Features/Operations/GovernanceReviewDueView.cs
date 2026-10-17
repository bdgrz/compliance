using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Reviews due in the selected period, with source and owner.</summary>
public sealed record GovernanceReviewDueView(Uuid TenantId, Uuid ProgramId, Uuid GovernanceReviewId, string Status, long Revision, DateTimeOffset UpdatedAt);
