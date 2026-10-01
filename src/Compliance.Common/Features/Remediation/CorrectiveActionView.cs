using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>Owned corrective work for a finding; status is open or completed.</summary>
public sealed record CorrectiveActionView(Uuid ActionId, string Description, Uuid OwnerMemberId,
    DateOnly DueOn, string Status, ActorReference AddedBy, DateTimeOffset AddedAt,
    string? ResolutionNotes = null, IReadOnlyList<EvidenceReference>? Evidence = null,
    Uuid? CompletedByMemberId = null, ActorReference? CompletedBy = null,
    DateTimeOffset? CompletedAt = null);
