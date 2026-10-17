using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.system-description.get-system-description", 1)]
public sealed record GetSystemDescription(Uuid TenantId, Uuid ProgramId, Uuid SystemDescriptionId)
    : IRequest<SystemDescriptionView>, IProgramReadRequest, ICallable;
