using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Enriches authorized member reads from canonical tenant and authenticated identity facts.</summary>
static class MemberPresentationEnrichment
{
    public static async ValueTask<Result<TeamMemberView>> WithTeamAsync(IDomainEventReader events,
        ITenantMembershipDirectoryReader memberships, IEmailAddressDirectoryReader? emails,
        IUserDisplayNameReader? profiles, IPersonMemberDisplayReader? people,
        Uuid tenantId, TeamMemberView member, CancellationToken ct)
    {
        Uuid? userId = null;
        var source = EventStreamPattern.ForPattern(tenantId.ToString(), "rbac-members",
            member.MemberId.ToString());
        await foreach (var record in events.ReadAsync(source, EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event is not MemberRegistered registered)
                continue;
            if (registered.TenantId != tenantId || registered.MemberId != member.MemberId ||
                registered.UserId == Uuid.Empty || RbacIds.Member(tenantId, registered.UserId) != member.MemberId)
                return Result<TeamMemberView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The team member was not found."));
            userId = registered.UserId;
            break;
        }
        // Historic team assignments may contain only an opaque member ID. Never infer its user.
        if (userId is null)
            return Result<TeamMemberView>.Success(member with { DisplayName = member.MemberId.ToString() });
        var membership = await memberships.GetAsync(tenantId.ToString(), userId.Value, ct)
            .ConfigureAwait(false);
        if (membership is null)
            return Result<TeamMemberView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The member projection has not reached the team member source.", isTransient: true));
        if (membership.TenantId != tenantId || membership.UserId != userId.Value)
            return Result<TeamMemberView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The team member was not found."));
        var presentation = await WithMemberAsync(emails, membership, ct, profiles, people)
            .ConfigureAwait(false);
        return Result<TeamMemberView>.Success(member with
        {
            UserId = userId,
            DisplayName = presentation.DisplayName,
            VerifiedEmailAddress = presentation.VerifiedEmailAddress,
        });
    }

    public static async ValueTask<TenantMembershipView> WithMemberAsync(
        IEmailAddressDirectoryReader? emails, TenantMembershipView member, CancellationToken ct,
        IUserDisplayNameReader? profiles = null, IPersonMemberDisplayReader? people = null)
    {
        string? cursor = null;
        string? verified = null;
        if (emails is not null)
        {
            do
            {
                var addresses = await emails.ListAsync(member.UserId, 20, cursor, ct).ConfigureAwait(false);
                foreach (var address in addresses.Items)
                    if (address.UserId == member.UserId && address.Verified &&
                        (verified is null || string.CompareOrdinal(address.EmailAddress, verified) < 0))
                        verified = address.EmailAddress;
                cursor = addresses.NextCursor;
            } while (cursor is not null);
        }
        var personName = people is null ? null : await people.ReadAsync(member.TenantId,
            member.UserId, ct).ConfigureAwait(false);
        var profileName = !string.IsNullOrWhiteSpace(personName) || profiles is null
            ? null : await profiles.ReadAsync(member.UserId, ct).ConfigureAwait(false);
        var name = !string.IsNullOrWhiteSpace(personName) ? personName
            : !string.IsNullOrWhiteSpace(profileName) ? profileName
            : verified ?? member.UserId.ToString();
        return member with { VerifiedEmailAddress = verified, DisplayName = name };
    }
}
