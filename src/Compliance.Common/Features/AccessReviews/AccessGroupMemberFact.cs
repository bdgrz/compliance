namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     An observed membership of one principal in a group, or the ability to assume a role when
///     the container is a role principal. Nesting is recorded only through these facts.
/// </summary>
public sealed record AccessGroupMemberFact(string GroupProviderSubjectId,
    string MemberProviderSubjectId);
