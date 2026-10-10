using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Microsoft.Extensions.Logging;

namespace Bdgrz.Compliance.Features.Work;

/// <summary>Finds current tenant members and advances their durable weekly digest records.</summary>
sealed partial class WorkDigestDispatchSweep(ITenantDirectoryReader tenants, ITenantActivity tenantActivity,
    ITenantMembershipDirectoryReader memberships, WorkDigestDispatchProcessor processor,
    ILogger<WorkDigestDispatchSweep> logger)
{
    public async ValueTask RunOnceAsync(CancellationToken ct)
    {
        string? tenantCursor = null;
        var tenantCursors = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var tenantsPage = await tenants.ListAsync(200, tenantCursor, ct).ConfigureAwait(false);
            foreach (var tenant in tenantsPage.Items)
            {
                if (tenant.TenantId == Uuid.Empty)
                    continue;
                var tenantActive = tenant.Status == "active" &&
                    await tenantActivity.IsActiveAsync(tenant.TenantId, ct).ConfigureAwait(false);
                await ProcessTenantMembersAsync(tenant.TenantId, tenantActive, ct)
                    .ConfigureAwait(false);
            }
            tenantCursor = tenantsPage.NextCursor;
            if (tenantCursor is not null && !tenantCursors.Add(tenantCursor))
                throw new InvalidOperationException("The tenant directory repeated a cursor.");
        } while (tenantCursor is not null);
    }

    async ValueTask ProcessTenantMembersAsync(Uuid tenantId, bool tenantActive,
        CancellationToken ct)
    {
        string? memberCursor = null;
        var memberCursors = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var membersPage = await memberships.ListAsync(tenantId, 200, memberCursor, ct)
                .ConfigureAwait(false);
            foreach (var membership in membersPage.Items)
            {
                if (membership.TenantId != tenantId || membership.UserId == Uuid.Empty)
                    continue;
                try
                {
                    await processor.ProcessMemberAsync(tenantId, membership.UserId, tenantActive,
                        !membership.IsSuspended && !membership.IsDeprovisioned, ct)
                        .ConfigureAwait(false);
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    // Never put SMTP details, addresses, digest text, or source data in logs.
                    LogMemberFailed(logger, tenantId, membership.UserId);
                }
            }
            memberCursor = membersPage.NextCursor;
            if (memberCursor is not null && !memberCursors.Add(memberCursor))
                throw new InvalidOperationException("The tenant membership directory repeated a cursor.");
        } while (memberCursor is not null);
    }

    [LoggerMessage(EventId = 41510, Level = LogLevel.Warning,
        Message = "Weekly digest processing failed for tenant {TenantId} and member {MemberId}.")]
    static partial void LogMemberFailed(ILogger logger, Uuid tenantId, Uuid memberId);
}
