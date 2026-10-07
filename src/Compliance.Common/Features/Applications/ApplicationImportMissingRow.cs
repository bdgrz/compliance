using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>A source omission proposal, never a staged row or authority to retire its target.</summary>
public sealed record ApplicationImportMissingRow(Uuid TenantId, Uuid BatchId, Uuid SourceClaimId,
    string SourceRecordId, Uuid ApplicationId, Uuid LastObservedBatchId,
    string Name, string Purpose, string? OwnerReference, DateTimeOffset ObservationCommittedAt,
    long ExpectedApplicationRevision, long? CurrentApplicationRevision,
    string MatchState, IReadOnlyList<string> AcceptanceBlockers);
