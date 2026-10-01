using Bdgrz.Compliance.Features.Workforce;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     Provider hints that only propose a classification: a non-human principal kind proposes
///     <c>nhi</c>, and a user account whose email matches exactly one roster person proposes
///     <c>human</c>. A match to several people is an ambiguous correlation and proposes nothing.
/// </summary>
public static class AccessPrincipalProposals
{
    public static (AccessPrincipalProposalView? Proposal, bool Ambiguous) Propose(
        AccessPrincipalFact principal, IReadOnlyList<PersonView> people)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(people);
        if (AccessReviewVocabulary.NonHumanKinds.Contains(principal.PrincipalKind))
            return (new AccessPrincipalProposalView(AccessReviewVocabulary.Nhi, null,
                $"The provider principal kind {principal.PrincipalKind} is non-human."), false);
        if (principal.PrincipalKind != AccessReviewVocabulary.UserAccount ||
            string.IsNullOrWhiteSpace(principal.Email))
            return (null, false);
        var matches = people.Where(person => string.Equals(person.WorkEmail?.Trim(),
            principal.Email.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length switch
        {
            1 => (new AccessPrincipalProposalView(AccessReviewVocabulary.Human, matches[0].PersonId,
                "The account email matches one roster person's work email."), false),
            > 1 => (null, true),
            _ => (null, false),
        };
    }
}
