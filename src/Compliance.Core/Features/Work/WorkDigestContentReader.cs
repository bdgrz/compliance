using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

enum WorkDigestContentReadKind
{
    Ready,
    Skip,
    Unavailable,
}

sealed record WorkDigestContentRead(WorkDigestContentReadKind Kind,
    WorkDigestDeliveryMessage? Message = null, string? SkipReason = null)
{
    public static WorkDigestContentRead Ready(WorkDigestDeliveryMessage message) =>
        new(WorkDigestContentReadKind.Ready, message);

    public static WorkDigestContentRead Skip(string reason) =>
        new(WorkDigestContentReadKind.Skip, SkipReason: reason);

    public static WorkDigestContentRead Unavailable() =>
        new(WorkDigestContentReadKind.Unavailable);
}

interface IWorkDigestContentReader
{
    ValueTask<WorkDigestContentRead> ReadAsync(Uuid tenantId, Uuid userId,
        WorkDigestDispatchStatusView dispatch, CancellationToken ct);
}

/// <summary>Rebuilds a digest from the member's current authorized work immediately before delivery.</summary>
sealed class WorkDigestContentReader(ITenantDirectoryReader tenants, ITenantActivity tenantActivity,
    ITenantMembershipDirectoryReader memberships, IEmailAddressDirectoryReader emails,
    IProgramDirectoryReader programs, IAccessGrantPermissionAuthorizer scopedPermissions,
    WorkQueueReader queue, IAggregateReader reader, WorkDigestDeliverySettings settings)
    : IWorkDigestContentReader
{
    public async ValueTask<WorkDigestContentRead> ReadAsync(Uuid tenantId, Uuid userId,
        WorkDigestDispatchStatusView dispatch, CancellationToken ct)
    {
        var memberId = RbacIds.Member(tenantId, userId);
        if (dispatch.TenantId != tenantId || dispatch.MemberId != memberId || !settings.IsReady)
            return WorkDigestContentRead.Unavailable();

        var tenant = await tenants.GetAsync(tenantId, ct).ConfigureAwait(false);
        if (tenant is null || tenant.TenantId != tenantId || tenant.Status != "active" ||
            !await tenantActivity.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return WorkDigestContentRead.Skip("tenant_inactive");

        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is null || membership.TenantId != tenantId || membership.UserId != userId ||
            membership.IsSuspended || membership.IsDeprovisioned)
            return WorkDigestContentRead.Skip("member_inactive");

        var preference = await ReadPreferenceAsync(reader, tenantId, memberId, ct)
            .ConfigureAwait(false);
        if (!preference.EmailDigestEnabled)
            return WorkDigestContentRead.Skip("email_opt_out");

        var recipient = await FirstVerifiedAddressAsync(userId, ct).ConfigureAwait(false);
        if (recipient is null)
            return WorkDigestContentRead.Skip("no_verified_email");

        var visiblePrograms = await scopedPermissions.GetProgramVisibilityAsync(tenantId,
                userId, memberId, IProgramReadRequest.ReadPermission, ct)
            .ConfigureAwait(false);
        var sourceItems = new List<WorkDigestSourceItem>();
        string? cursor = null;
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var page = await programs.ListAsync(tenantId, 200, cursor, ct).ConfigureAwait(false);
            foreach (var program in page.Items)
            {
                if (program.TenantId != tenantId)
                    return WorkDigestContentRead.Unavailable();
                if (!visiblePrograms.OrganizationWide &&
                    !visiblePrograms.ProgramIds.Contains(program.ProgramId))
                    continue;
                var actor = new OperationsActor(userId, memberId,
                    membership.DisplayName ?? userId.ToString());
                var work = await queue.ReadForDigestAsync(tenantId, program.ProgramId, actor,
                    dispatch.WeekOf, 7, ct).ConfigureAwait(false);
                if (!work.IsSuccess)
                    return WorkDigestContentRead.Unavailable();
                sourceItems.AddRange(work.Value.Entries.Select(entry =>
                    new WorkDigestSourceItem(program.ProgramId, program.Name, entry.Item)));
            }
            cursor = page.NextCursor;
            if (cursor is not null && !seenCursors.Add(cursor))
                return WorkDigestContentRead.Unavailable();
        } while (cursor is not null);

        var message = WorkDigestContentComposer.Compose(tenantId, memberId, dispatch.WeekOf,
            tenant.Slug, dispatch.MessageId, recipient, sourceItems, settings);
        return message is null
            ? WorkDigestContentRead.Skip("empty_digest")
            : WorkDigestContentRead.Ready(message);
    }

    static async ValueTask<WorkDigestPreferenceView> ReadPreferenceAsync(IAggregateReader reader,
        Uuid tenantId,
        Uuid memberId, CancellationToken ct)
    {
        // The default aggregate reads as subscribed in UTC until the member records a preference.
        // The schedule is already frozen in the dispatch record, so a timezone edit cannot move it.
        var preference = await reader.HydrateAsync(new WorkDigestPreference(tenantId, memberId), ct)
            .ConfigureAwait(false);
        return preference.Read();
    }

    async ValueTask<string?> FirstVerifiedAddressAsync(Uuid userId, CancellationToken ct)
    {
        var verified = new List<string>();
        string? cursor = null;
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var page = await emails.ListAsync(userId, 200, cursor, ct).ConfigureAwait(false);
            verified.AddRange(page.Items.Where(address => address.UserId == userId && address.Verified)
                .Select(static address => address.EmailAddress));
            cursor = page.NextCursor;
            if (cursor is not null && !seenCursors.Add(cursor))
                return null;
        } while (cursor is not null);
        return verified.Order(StringComparer.Ordinal).FirstOrDefault();
    }
}
