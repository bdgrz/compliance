using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.examinations.get-management-assertion", 1)]
public sealed record GetManagementAssertion(Uuid TenantId, Uuid ProgramId, Uuid ManagementAssertionId)
    : IRequest<ManagementAssertionView>, IProgramReadRequest, ICallable;
