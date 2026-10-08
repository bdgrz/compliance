using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record ServiceEngagementDraftContent(string Practice, string Scope,
    DateOnly PeriodStart, DateOnly? PeriodEnd, Uuid EngagementLeadStaffMemberId);
