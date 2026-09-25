using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A past or ongoing advisory engagement the firm delivered to one client.</summary>
public sealed record AdvisoryEngagementRecord(Uuid ClientTenantId, AdvisoryService Service, DateOnly? EndedOn);
