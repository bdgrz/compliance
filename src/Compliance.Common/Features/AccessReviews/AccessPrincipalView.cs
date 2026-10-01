using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     One accepted principal with its current classification, decision history, proposal, and
///     open gaps. Groups and roles are access structures and are never classified.
/// </summary>
public sealed record AccessPrincipalView(Uuid PopulationId, string ProviderSubjectId,
    string PrincipalKind, string DisplayName, string Status, string? Email,
    bool IsAccessStructure, string Classification,
    AccessPrincipalClassificationView? Current,
    IReadOnlyList<AccessPrincipalClassificationView> History,
    AccessPrincipalProposalView? Proposal, IReadOnlyList<string> Gaps, int EntitlementCount);
