using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

[Discriminator("bdgrz.workforce.person.list", 1)]
public sealed record ListPeople(Uuid TenantId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<PersonView>>, IWorkforceRequest, ICallable;
