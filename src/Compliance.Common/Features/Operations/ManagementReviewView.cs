using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Management review record, inputs, participants, and completion state.</summary>
public sealed record ManagementReviewView(Uuid TenantId, Uuid ProgramId, Uuid ManagementReviewId, string Status, long Revision, DateTimeOffset UpdatedAt);
