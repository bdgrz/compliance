using System.Globalization;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.TechnologyInventory;

static class TechnologyInventoryDirectorySchema
{
    public static readonly KvDirectoryIndex<TechnologyComponentView> ComponentsByName = new(
        "by_name", 1, static view =>
            [view.Content.Name.ToUpperInvariant(), view.ComponentId.ToString()]);

    public static readonly KvDirectory<TechnologyComponentView, Uuid> Components = new(
        "technology_components", ComplianceCoreJsonContext.Default.TechnologyComponentView,
        static view => view.ComponentId, static id => [id.ToString()], [ComponentsByName]);

    public static readonly KvDirectoryIndex<TechnologyComponentView> ComponentRevisionsById =
        new("by_component", 1, static view =>
            [view.ComponentId.ToString(), Padded(view.Revision)]);

    public static readonly KvDirectory<TechnologyComponentView, string> ComponentRevisions = new(
        "technology_component_revisions",
        ComplianceCoreJsonContext.Default.TechnologyComponentView,
        static view => RevisionKey(view.ComponentId, view.Revision), static key => [key],
        [ComponentRevisionsById]);

    public static readonly KvDirectoryIndex<InformationAssetView> AssetsByName = new(
        "by_name", 1, static view =>
            [view.Content.Name.ToUpperInvariant(), view.InformationAssetId.ToString()]);

    public static readonly KvDirectory<InformationAssetView, Uuid> Assets = new(
        "information_assets", ComplianceCoreJsonContext.Default.InformationAssetView,
        static view => view.InformationAssetId, static id => [id.ToString()], [AssetsByName]);

    public static readonly KvDirectoryIndex<InformationAssetView> AssetRevisionsById =
        new("by_asset", 1, static view =>
            [view.InformationAssetId.ToString(), Padded(view.Revision)]);

    public static readonly KvDirectory<InformationAssetView, string> AssetRevisions = new(
        "information_asset_revisions", ComplianceCoreJsonContext.Default.InformationAssetView,
        static view => RevisionKey(view.InformationAssetId, view.Revision), static key => [key],
        [AssetRevisionsById]);

    public static readonly KvDirectoryIndex<DataFlowView> FlowsByPurpose = new(
        "by_purpose", 1, static view =>
            [view.Content.Purpose.ToUpperInvariant(), view.DataFlowId.ToString()]);

    public static readonly KvDirectory<DataFlowView, Uuid> Flows = new(
        "data_flows", ComplianceCoreJsonContext.Default.DataFlowView,
        static view => view.DataFlowId, static id => [id.ToString()], [FlowsByPurpose]);

    public static readonly KvDirectoryIndex<DataFlowView> FlowRevisionsById =
        new("by_flow", 1, static view => [view.DataFlowId.ToString(), Padded(view.Revision)]);

    public static readonly KvDirectory<DataFlowView, string> FlowRevisions = new(
        "data_flow_revisions", ComplianceCoreJsonContext.Default.DataFlowView,
        static view => RevisionKey(view.DataFlowId, view.Revision), static key => [key],
        [FlowRevisionsById]);

    static string RevisionKey(Uuid id, long revision) => $"{id}:{Padded(revision)}";

    static string Padded(long revision) =>
        revision.ToString("D20", CultureInfo.InvariantCulture);
}
