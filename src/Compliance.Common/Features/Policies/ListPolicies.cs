using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

[Discriminator("bdgrz.policies.list", 1)]
public sealed record ListPolicies(Uuid TenantId, Uuid ProgramId, int? Limit = null,
    string? Cursor = null)
    : IRequest<Page<PolicySummaryView>>, IProgramReadRequest, ICallable;
