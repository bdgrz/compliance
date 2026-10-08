using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Manually records one immutable source revision, explicitly correlated to a governed record.</summary>
[Discriminator("bdgrz.workforce.source.record", 1)]
public sealed record RecordWorkforceSourceObservation(Uuid TenantId, WorkforceSourceIdentity Source,
    string TargetKind, Uuid TargetId, long ExpectedTargetRevision, WorkforceSourceFacts Facts,
    DateTimeOffset ObservedAt) : IRequest<WorkforceSourceRegistration>, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
