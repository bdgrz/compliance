using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>A declared relationship, not an approval of downstream scope.</summary>
public sealed record ProviderDependency(string SubjectKind, Uuid? SubjectId,
    Uuid? ProgramId, Uuid? ApplicationId, string Rationale, DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntilExclusive = null, string? UnresolvedReference = null,
    ProviderSourceCitation? SourceCitation = null)
{
    public long? SourceRevision { get; init; }
    public long? ProgramRevision { get; init; }
    public long? ApplicationRevision { get; init; }
}
