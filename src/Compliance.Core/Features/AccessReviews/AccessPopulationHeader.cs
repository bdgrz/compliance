using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>The frozen identity of an accepted population: what was observed, where, when, and how.</summary>
public sealed record AccessPopulationHeader(Uuid PopulationId, Uuid ApplicationId,
    Uuid SystemInstanceId, long SystemInstanceRevision, DateTimeOffset ObservedAt,
    string SourceKind, string Source, string Attestation);
