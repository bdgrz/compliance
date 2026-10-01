using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

/// <summary>
///     The downstream impact of approving the exact draft revision over its predecessor: changed
///     fields, applicability relationships added, removed, and retained, and the distribution
///     campaigns bound to the predecessor, which keep their exact version. <c>Digest</c> must be
///     acknowledged when approving a successor.
/// </summary>
public sealed record PolicyImpactPreview(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long Revision, long? PredecessorVersion, IReadOnlyList<string> ChangedFields,
    IReadOnlyList<PolicyApplicabilityReference> AddedApplicability,
    IReadOnlyList<PolicyApplicabilityReference> RemovedApplicability,
    IReadOnlyList<PolicyApplicabilityReference> RetainedApplicability,
    IReadOnlyList<Uuid> AffectedCampaignIds, bool AudienceChanged, string Digest);
