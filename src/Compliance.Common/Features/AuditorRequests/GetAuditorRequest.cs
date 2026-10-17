using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.auditor-requests.get-auditor-request", 1)]
public sealed record GetAuditorRequest(Uuid TenantId, Uuid ProgramId, Uuid AuditorRequestId)
    : IRequest<AuditorRequestView>, IProgramReadRequest, ICallable;
