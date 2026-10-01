using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Reads one source observation with restricted fields disclosed only by explicit field grants.</summary>
[Discriminator("bdgrz.workforce.source.get", 1)]
public sealed record GetWorkforceSourceObservation(Uuid TenantId, Uuid ObservationId,
    long? MinimumRevision = null) : IRequest<WorkforceSourceView>, IWorkforceRequest, ICallable;
