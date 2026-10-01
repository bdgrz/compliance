namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     The governed content of one policy draft or version. <c>Body</c> holds the policy text
///     inside the bounded event payload; <c>SourceReference</c> names an external document until
///     governed policy artifacts exist. <c>AudienceKind</c> is <c>core_security</c> or
///     <c>role_targeted</c>; a role-targeted policy names roster teams (M0-D12).
/// </summary>
public sealed record PolicyContent(string Title, string Purpose, string AudienceKind,
    IReadOnlyList<string>? AudienceTeams, int ReviewCadenceMonths, string? Body,
    string? SourceReference, string? OwnerReference,
    IReadOnlyList<PolicyApplicabilityReference>? Applicability);
