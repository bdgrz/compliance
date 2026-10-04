using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>Exact provider coverage facts and the source revision that supports them.</summary>
public sealed record ProviderCoverageGapContent(Uuid ServiceId, string Service,
    string Assertion, DateOnly PeriodStart, DateOnly PeriodEnd, string SourceKind,
    Uuid SourceId, long SourceRevision, string Description);
