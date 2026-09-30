using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

/// <summary>Recomputes a flow's carried classification under a proposed asset change (M0-D08).</summary>
public static class InformationAssetImpact
{
    public static DataFlowImpactView Evaluate(DataFlowView flow, Uuid changedAssetId,
        string proposedClassification, string proposedLifecycle,
        IReadOnlyDictionary<Uuid, InformationAssetContent> carried)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(carried);
        var classifications = new List<string>();
        var anyActive = false;
        foreach (var assetId in flow.Content.InformationAssetIds)
        {
            var (classification, lifecycle) = assetId == changedAssetId
                ? (proposedClassification, proposedLifecycle)
                : carried.TryGetValue(assetId, out var content)
                    ? (content.Classification, content.Lifecycle)
                    // An unknown asset is ranked by the flow's recorded value so it never lowers it.
                    : (flow.Content.Classification, TechnologyInventoryRules.Active);
            classifications.Add(classification);
            anyActive |= lifecycle == TechnologyInventoryRules.Active;
        }
        var recomputed = classifications.MaxBy(TechnologyInventoryRules.Rank) ??
                         flow.Content.Classification;
        var violation = TechnologyInventoryRules.RequiresEncryption(recomputed) &&
                        !(flow.Content.EncryptedInTransit && flow.Content.EncryptedAtRest) &&
                        flow.Content.ExceptionReference is null;
        return new DataFlowImpactView(flow.DataFlowId, flow.Revision, flow.Content.Classification,
            recomputed, recomputed != flow.Content.Classification,
            flow.Content.EncryptedInTransit, flow.Content.EncryptedAtRest,
            flow.Content.ExceptionReference, violation, !anyActive);
    }
}
