using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.stage", 1)]
public sealed record StageApplicationImport(Uuid TenantId, Uuid SubmissionId,
    string SourceKey, string SourceNamespace, string Coverage,
    IReadOnlyList<ApplicationImportInputRow> Rows)
    : IRequest<ApplicationImportRegistration>, IApplicationInventoryRequest, ICallable;
