using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>One person's assignment to one client engagement.</summary>
public sealed record EngagementAssignment(
    Uuid ClientTenantId,
    Uuid EngagementId,
    Uuid UserId,
    EngagementPractice Practice);
