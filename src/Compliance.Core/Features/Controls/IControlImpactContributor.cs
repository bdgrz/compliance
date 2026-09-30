using Bdgrz.Compliance.Features.Versioning;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     An owning context's bounded, tenant-scoped contribution to a Control successor or
///     retirement impact preview. Contexts without a registered contributor are reported as
///     unlinked because no record in them can reference a Control yet.
/// </summary>
public interface IControlImpactContributor :
    IImpactContributor<ControlDraft, ControlChange, ControlImpactContribution>;
