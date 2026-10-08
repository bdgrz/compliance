using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.omission.preview", 1)]
public sealed record PreviewApplicationImportOmissionProposal(Uuid TenantId, Uuid BatchId)
    : IRequest<ApplicationImportOmissionPreview>, IApplicationInventoryRequest, ICallable;
