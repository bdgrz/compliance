using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.changes.get-significant-change", 1)]
public sealed record GetSignificantChange(Uuid TenantId, Uuid ProgramId, Uuid SignificantChangeId)
    : IRequest<SignificantChangeView>, IProgramReadRequest, ICallable;
