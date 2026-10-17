using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.advisor-collaboration.get-advisor-request", 1)]
public sealed record GetAdvisorRequest(Uuid TenantId, Uuid ProgramId, Uuid AdvisorRequestId)
    : IRequest<AdvisorRequestView>, IProgramReadRequest, ICallable;
