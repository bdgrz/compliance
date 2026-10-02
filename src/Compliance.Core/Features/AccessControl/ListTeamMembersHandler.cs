using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.UserIdentities;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class ListTeamMembersHandler(ITeamMemberDirectoryReader directory,
    IDomainEventReader? events = null, ITenantMembershipDirectoryReader? memberships = null,
    IEmailAddressDirectoryReader? emails = null, IUserDisplayNameReader? profiles = null,
    IPersonMemberDisplayReader? people = null)
    : IRequestHandler<ListTeamMembers, Page<TeamMemberView>>
{
    public async ValueTask<Result<Page<TeamMemberView>>> HandleAsync(
        IRequestContext<ListTeamMembers> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<TeamMemberView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The team member list limit must be between 1 and 200."));
        Page<TeamMemberView> page;
        try
        {
            page = await directory.ListAsync(
                request.TenantId,
                request.TeamId,
                request.Limit ?? 50,
                request.Cursor,
                ListRequestNormalization.NormalizeSearch(request.Search),
                ListRequestNormalization.IsDescending(request.Sort),
                ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<TeamMemberView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The team member cursor is invalid."));
        }
        if (page.Items.Any(item => item.TeamId != request.TeamId))
            return Result<Page<TeamMemberView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The team members were not found."));
        if (events is null || memberships is null)
            return Result<Page<TeamMemberView>>.Success(page);
        var enriched = new List<TeamMemberView>(page.Items.Count);
        foreach (var member in page.Items)
        {
            var result = await MemberPresentationEnrichment.WithTeamAsync(events, memberships,
                emails, profiles, people, request.TenantId, member, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return Result<Page<TeamMemberView>>.Failure(result.Error);
            enriched.Add(result.Value);
        }
        return Result<Page<TeamMemberView>>.Success(new Page<TeamMemberView>(enriched, page.NextCursor));
    }
}
