using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>
/// The active data flows that carry the asset, with each flow's classification recomputed from
/// the proposed asset classification. <see cref="FlowsOverLimit"/> is true when the scan stopped
/// before every flow was inspected, so the list is never presented as complete when it is not.
/// </summary>
public sealed record InformationAssetChangePreview(Uuid TenantId, Uuid InformationAssetId,
    long Revision, string CurrentClassification, string ProposedClassification,
    string CurrentLifecycle, string ProposedLifecycle,
    IReadOnlyList<DataFlowImpactView> AffectedFlows, bool FlowsOverLimit);
