using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.program.revisions.list", 1)]
public sealed record ListProgramRevisions(Uuid TenantId, Uuid ProgramId,
    int? Limit = null, string? Cursor = null, long? MinimumProgramRevision = null)
    : IRequest<Page<ProgramRevisionView>>, ITenantAccessRequest, ICallable;
