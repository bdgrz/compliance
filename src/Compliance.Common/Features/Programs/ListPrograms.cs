using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

[Discriminator("bdgrz.program.list", 1)]
public sealed record ListPrograms(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<ProgramView>>, ITenantAccessRequest, ICallable;
