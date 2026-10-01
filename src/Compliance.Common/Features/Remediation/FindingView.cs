using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     A governed deficiency and its corrective work. Status is open, remediated, or closed.
///     Readiness status is closed, remediated, accepted, overdue, or unresolved when read.
/// </summary>
public sealed record FindingView(Uuid TenantId, Uuid ProgramId, Uuid FindingId, long Revision,
    FindingSource Source, string Title, string Description, string Severity,
    string AffectedScope, Uuid OwnerMemberId, DateOnly DueOn, string? RootCause, string Status,
    string ReadinessStatus, IReadOnlyList<FindingLink> Links,
    IReadOnlyList<CorrectiveActionView> CorrectiveActions,
    IReadOnlyList<FindingAcceptanceView> Acceptances, FindingClosureView? Closure,
    IReadOnlyList<FindingHistoryEntryView> History, ActorReference RaisedBy,
    DateTimeOffset RaisedAt);
