using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

[Discriminator("bdgrz.application_import.preview", 1)]
public sealed record PreviewApplicationImport(Uuid TenantId, Uuid BatchId,
    int? Limit = null, string? Cursor = null, long? MinimumRevision = null)
    : IRequest<Page<ApplicationImportPreviewRow>>, IApplicationInventoryRequest, ICallable;
