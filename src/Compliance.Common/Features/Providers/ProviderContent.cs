using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Authored supplier facts; registration is not an assurance or scope approval.</summary>
public sealed record ProviderContent(string Name, string ProviderKind,
    string? Materiality = null, IReadOnlyList<string>? MaterialityBasis = null,
    string? MaterialityRationale = null, bool Subservice = false,
    string? BoundaryTreatment = null, string? BoundaryTreatmentRationale = null,
    Uuid? OwnerPersonId = null, string? OwnerReference = null,
    IReadOnlyList<ProviderDependency>? Dependencies = null,
    ProviderSourceCitation? SourceCitation = null)
{
    public long? OwnerPersonRevision { get; init; }
}
