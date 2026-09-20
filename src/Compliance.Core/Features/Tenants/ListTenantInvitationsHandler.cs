using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

public sealed class ListTenantInvitationsHandler(ITenantInvitationDirectoryReader directory,
    ITenantMembershipDirectoryReader memberships, IMemberAccessReader access,
    TimeProvider clock) : IRequestHandler<ListTenantInvitations, Page<TenantInvitationView>>
{
    public async ValueTask<Result<Page<TenantInvitationView>>> HandleAsync(
        IRequestContext<ListTenantInvitations> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.ExpectedStatus is not null &&
            request.ExpectedStatus is not ("pending" or "expired" or
                "accepted_pending_activation" or "active"))
            return Failure(RequestErrorKind.Validation, "The expected status is not supported.");
        if (request.ExpectedStatus is not null && request.EmailAddress is null)
            return Failure(RequestErrorKind.Validation,
                "An email address is required when expecting an invitation status.");

        var now = clock.GetUtcNow();
        if (request.EmailAddress is not null)
        {
            if (!EmailAddresses.TryNormalize(request.EmailAddress, out var normalized))
                return Failure(RequestErrorKind.Validation, "Enter a valid email address.");
            var invitation = await directory.GetAsync(request.TenantId, normalized, ct)
                .ConfigureAwait(false);
            if (invitation is null)
                return request.ExpectedStatus is null
                    ? Result<Page<TenantInvitationView>>.Success(new Page<TenantInvitationView>([], null))
                    : Failure(RequestErrorKind.Conflict,
                        "The expected invitation has not reached the projection.");
            var view = await ToViewAsync(invitation, now, ct).ConfigureAwait(false);
            if (request.ExpectedStatus is not null && view.Status != request.ExpectedStatus)
                return Failure(RequestErrorKind.Conflict,
                    "The expected invitation status has not reached the projection.");
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
            {
                var expectedRoleId = entry.Administrator
                    ? BuiltInRbac.TenantAdministrationRoleId(entry.TenantId)
                    : entry.BuiltInRole is { } role
                        ? BuiltInRbac.RoleIdForRole(entry.TenantId, role)
                        : null;
                if (expectedRoleId is null)
                    status = "active";
                else
                {
                    var edges = await access.ReadAsync(entry.TenantId,
                        RbacIds.Member(entry.TenantId, userId), ct).ConfigureAwait(false);
                    if (edges.Any(edge => edge.RoleId == expectedRoleId &&
                        edge.Permissions.Contains(RbacPermissions.TenantAccess,
                            StringComparer.Ordinal)))
                        status = "active";
                }
            }
        }
        return new TenantInvitationView(entry.TenantId, entry.EmailAddress,
            entry.Affiliation, entry.Administrator, entry.BuiltInRole, status,
            entry.ExpiresAt, entry.InvitedBy, entry.AcceptedUserId);
    }

    static Result<Page<TenantInvitationView>> Failure(RequestErrorKind kind, string message) =>
        Result<Page<TenantInvitationView>>.Failure(new RequestError(kind, message));
}
