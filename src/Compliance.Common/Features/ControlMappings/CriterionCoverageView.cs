using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

/// <summary>
///     Mapping coverage for one catalog entry: mapped when at least one accepted mapping exists,
///     otherwise unmapped. Pending proposals never count. Coverage does not assess whether the
///     criterion is satisfied, whether controls operate, or whether evidence exists.
/// </summary>
public sealed record CriterionCoverageView(Uuid EditionId, string Identifier, string Kind,
    string Category, string? ParentIdentifier, string Summary, string CoverageState,
    IReadOnlyList<MappedControlReference> MappedControls, int PendingProposalCount);
