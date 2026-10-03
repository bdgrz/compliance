using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Records accepted provenance after canonical facts match, or dismisses conflicting facts with
///     an attributable note. This personal decision is HTTP-only. Never changes canonical facts or
///     platform/provider access.
/// </summary>
[Discriminator("bdgrz.workforce.source.reconcile", 1)]
public sealed record ReconcileWorkforceSourceObservation(Uuid TenantId, Uuid ObservationId,
    long ExpectedRevision, long ExpectedTargetRevision, string Outcome, string Note)
    : IRequest, IWorkforceRequest, ICallable;
