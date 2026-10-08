using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record HistoricalEngagementAssignmentView(Uuid ClientTenantId, Uuid EngagementId,
    Uuid FirmStaffMemberId, Uuid UserId, string Practice);
