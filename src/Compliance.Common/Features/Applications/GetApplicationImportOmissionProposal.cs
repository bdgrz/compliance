using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Reads only the still-current durable proposal; no unrestricted historical read is implied.</summary>
[Discriminator("bdgrz.application_import.omission.get", 1)]
public sealed record GetApplicationImportOmissionProposal(Uuid TenantId, Uuid BatchId)
    : IRequest<ApplicationImportOmissionProposalView>, IApplicationInventoryRequest, ICallable;
