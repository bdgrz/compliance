using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>
///     Closes a joiner, mover, leaver, or roster reconciliation observation with an attributed
///     <c>Resolution</c> of <c>resolved</c> or <c>dismissed</c> and a note of at most 1000
///     characters. This personal decision is HTTP-only. Resolving is compliance record-keeping
///     only: it never changes platform or provider access. An identical retry succeeds; a different
///     closure conflicts.
/// </summary>
[Discriminator("bdgrz.workforce.observation.resolve", 1)]
public sealed record ResolveWorkforceObservation(Uuid TenantId, Uuid ObservationId,
    string Resolution, string Note) : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
