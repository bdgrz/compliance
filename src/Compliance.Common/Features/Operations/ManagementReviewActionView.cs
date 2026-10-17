using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Actions arising from a review and their ownership/status.</summary>
public sealed record ManagementReviewActionView(Uuid TenantId, Uuid ProgramId, Uuid ManagementReviewActionId, string Status, long Revision, DateTimeOffset UpdatedAt);
