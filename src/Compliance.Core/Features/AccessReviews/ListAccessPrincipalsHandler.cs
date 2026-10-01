using Bdgrz.Compliance.Features.Controls;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Lists accepted principals with their classification, proposal, and open gaps. Gaps are
///     <c>unclassified</c>, <c>ambiguous_correlation</c>, <c>inactive_worker</c>,
///     <c>shared_account</c>, <c>nhi_unowned</c>, <c>nhi_expired</c>, and <c>disabled_with_access</c>;
///     groups and roles report <c>nested_group</c> when they contain another access structure.
/// </summary>
public sealed class ListAccessPrincipalsHandler(IAggregateReader reader,
    IAccessReviewSources sources)
    : IRequestHandler<ListAccessPrincipals, Page<AccessPrincipalView>>
{
    public async ValueTask<Result<Page<AccessPrincipalView>>> HandleAsync(
        IRequestContext<ListAccessPrincipals> context, CancellationToken ct)
    {
        var request = context.Request;
        var loaded = await AcceptedAccessPopulation.LoadAsync(reader, request.TenantId,
            request.PopulationId, ct).ConfigureAwait(false);
        if (!loaded.IsSuccess)
            return Result<Page<AccessPrincipalView>>.Failure(loaded.Error);
        var accepted = loaded.Value;
        var people = await sources.ListPeopleAsync(request.TenantId, ct).ConfigureAwait(false);
        if (!people.IsSuccess)
            return Result<Page<AccessPrincipalView>>.Failure(people.Error);
        var kinds = accepted.Facts.Principals.ToDictionary(static principal =>
            principal.ProviderSubjectId, static principal => principal.PrincipalKind, StringComparer.Ordinal);

        var views = new List<AccessPrincipalView>();
        foreach (var principal in accepted.Facts.Principals.OrderBy(static principal =>
                     principal.ProviderSubjectId, StringComparer.Ordinal))
        {
            var structure = AccessReviewVocabulary.IsAccessStructure(principal.PrincipalKind);
            var access = accepted.EffectiveAccess.Count(row =>
                row.ProviderSubjectId == principal.ProviderSubjectId);
            var current = accepted.Population.CurrentClassification(principal.ProviderSubjectId);
            var (proposal, ambiguous) = structure
                ? (null, false)
                : AccessPrincipalProposals.Propose(principal, people.Value);
            var gaps = new List<string>();
            if (structure)
            {
                if (accepted.Facts.GroupMembers.Any(member =>
                        member.GroupProviderSubjectId == principal.ProviderSubjectId &&
                        kinds.TryGetValue(member.MemberProviderSubjectId, out var kind) &&
                        AccessReviewVocabulary.IsAccessStructure(kind)))
                    gaps.Add("nested_group");
            }
            else
            {
                var failure = await AddGapsAsync(request.TenantId, current, gaps, ct).ConfigureAwait(false);
                if (failure is not null)
                    return Result<Page<AccessPrincipalView>>.Failure(failure);
                if (ambiguous)
                    gaps.Add("ambiguous_correlation");
                if (access > 0 && principal.Status != "active")
                    gaps.Add("disabled_with_access");
            }
            if (request.Gap is { } wanted && !gaps.Contains(wanted))
                continue;
            views.Add(new AccessPrincipalView(accepted.Population.Id, principal.ProviderSubjectId,
                principal.PrincipalKind, principal.DisplayName, principal.Status, principal.Email,
                structure, structure ? principal.PrincipalKind : current?.Classification ??
                    AccessReviewVocabulary.Unclassified, current,
                accepted.Population.History(principal.ProviderSubjectId), proposal, gaps, access));
        }
        return ControlActivationSource.Paginate(views, request.Limit, request.Cursor,
            "access principal");
    }

    async ValueTask<RequestError?> AddGapsAsync(Uuid tenantId,
        AccessPrincipalClassificationView? current, List<string> gaps, CancellationToken ct)
    {
        switch (current?.Classification)
        {
            case null or AccessReviewVocabulary.Unclassified:
                gaps.Add("unclassified");
                break;
            case AccessReviewVocabulary.Human when current.PersonId is { } personId:
                {
                    var statuses = await sources.RelationshipStatusesAsync(tenantId, personId, ct)
                        .ConfigureAwait(false);
                    if (!statuses.IsSuccess)
                        return statuses.Error;
                    if (!statuses.Value.Any(static status => status is "active" or "on_leave"))
                        gaps.Add("inactive_worker");
                    break;
                }
            case AccessReviewVocabulary.Shared:
                gaps.Add("shared_account");
                break;
            case AccessReviewVocabulary.Nhi when current.ServiceIdentityId is { } identityId:
                {
                    var identity = await sources.GetServiceIdentityAsync(tenantId, identityId, ct)
                        .ConfigureAwait(false);
                    if (!identity.IsSuccess)
                        return identity.Error;
                    if (identity.Value.Unowned)
                        gaps.Add("nhi_unowned");
                    if (identity.Value.Expired)
                        gaps.Add("nhi_expired");
                    break;
                }
        }
        return null;
    }
}
