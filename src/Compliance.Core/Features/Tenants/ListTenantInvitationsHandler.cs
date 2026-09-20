using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ListTenantInvitationsHandler(ITenantInvitationDirectoryReader directory,
    ITenantMembershipDirectoryReader memberships,
    TimeProvider clock) : IRequestHandler<ListTenantInvitations, Page<TenantInvitationView>>
{
    public async ValueTask<Result<Page<TenantInvitationView>>> HandleAsync(
        IRequestContext<ListTenantInvitations> context, CancellationToken ct)
    {
        var request = context.Request;
        var now = clock.GetUtcNow();
        if (request.EmailAddress is not null)
        {
            if (!EmailAddresses.TryNormalize(request.EmailAddress, out var normalized))
                return Failure(RequestErrorKind.Validation, "Enter a valid email address.");
            var invitation = await directory.GetAsync(request.TenantId, normalized, ct)
                .ConfigureAwait(false);
            if (invitation is null)
                return Result<Page<TenantInvitationView>>.Success(new Page<TenantInvitationView>([], null));
            var view = await ToViewAsync(invitation, now, ct).ConfigureAwait(false);
            return Result<Page<TenantInvitationView>>.Success(new Page<TenantInvitationView>([view], null));
        }

        var page = await directory.ListAsync(request.TenantId,
            Math.Clamp(request.Limit ?? 50, 1, 200), request.Cursor, ct).ConfigureAwait(false);
        var views = new List<TenantInvitationView>(page.Items.Count);
        foreach (var entry in page.Items)
            views.Add(await ToViewAsync(entry, now, ct).ConfigureAwait(false));
        return Result<Page<TenantInvitationView>>.Success(new Page<TenantInvitationView>(
            views, page.NextCursor));
    }

    async ValueTask<TenantInvitationView> ToViewAsync(TenantInvitationDirectoryEntry entry,
        DateTimeOffset now, CancellationToken ct)
    {
        var status = now >= entry.ExpiresAt ? "expired" : "pending";
        if (entry.AcceptedUserId is { } userId)
        {
            status = "accepted_pending_activation";
            if (await memberships.IsMemberAsync(entry.TenantId.ToString(), userId, ct)
                    .ConfigureAwait(false))
                status = "active";
        }
        return new TenantInvitationView(entry.TenantId, entry.EmailAddress,
            entry.Affiliation, entry.Administrator, entry.BuiltInRole, status,
            entry.ExpiresAt, entry.InvitedBy, entry.AcceptedUserId);
    }

    static Result<Page<TenantInvitationView>> Failure(RequestErrorKind kind, string message) =>
        Result<Page<TenantInvitationView>>.Failure(new RequestError(kind, message));
}
