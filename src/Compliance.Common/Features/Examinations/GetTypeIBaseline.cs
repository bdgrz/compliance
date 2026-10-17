using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.examinations.get-type-i-baseline", 1)]
public sealed record GetTypeIBaseline(Uuid TenantId, Uuid ProgramId, Uuid TypeIBaselineId)
    : IRequest<TypeIBaselineView>, IProgramReadRequest, ICallable;
