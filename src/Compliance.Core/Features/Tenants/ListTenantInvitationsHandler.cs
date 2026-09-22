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
        if (request.Limit is < 1 or > 200)
            return Failure(RequestErrorKind.Validation,
                "The tenant invitation list limit must be between 1 and 200.");
        if (request.EmailAddress is not null && request.Cursor is not null)
            return Failure(RequestErrorKind.Validation,
                "The tenant invitation cursor cannot be used with an email address filter.");
        var now = clock.GetUtcNow();
        if (request.EmailAddress is not null)
        {
            if (!EmailAddresses.TryNormalize(request.EmailAddress, out var normalized))
                return Failure(RequestErrorKind.Validation, "Enter a valid email address.");
            var invitation = await directory.GetAsync(request.TenantId, normalized, ct)
                .ConfigureAwait(false);
            if (invitation is null)
                return Result<Page<TenantInvitationView>>.Success(new Page<TenantInvitationView>([], null));
            if (invitation.TenantId != request.TenantId)
                return Failure(RequestErrorKind.NotFound, "The tenant invitations were not found.");
            var view = await ToViewAsync(invitation, now, ct).ConfigureAwait(false);
            return Result<Page<TenantInvitationView>>.Success(new Page<TenantInvitationView>([view], null));
        }

        Page<TenantInvitationDirectoryEntry> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Failure(RequestErrorKind.Validation, "The tenant invitation cursor is invalid.");
        }
        if (page.Items.Any(invitation => invitation.TenantId != request.TenantId))
            return Failure(RequestErrorKind.NotFound, "The tenant invitations were not found.");
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
            entry.ExpiresAt, entry.InvitedBy, entry.AcceptedUserId)
        {
            DeliveryStatus = entry.DeliveryStatus,
        };
    }

    static Result<Page<TenantInvitationView>> Failure(RequestErrorKind kind, string message) =>
        Result<Page<TenantInvitationView>>.Failure(new RequestError(kind, message));
}
