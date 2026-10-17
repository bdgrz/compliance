using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.auditor-requests.get-sample-selection", 1)]
public sealed record GetSampleSelection(Uuid TenantId, Uuid ProgramId, Uuid SampleSelectionId)
    : IRequest<SampleSelectionView>, IProgramReadRequest, ICallable;
