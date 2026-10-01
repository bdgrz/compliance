using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Reference metadata only; no source content or assurance assertion.</summary>
public sealed record ProviderSourceCitation(string ArtifactKind, string Title,
    string VersionOrDate, string Locator, string MetadataClassification,
    Uuid? ArtifactId = null);
