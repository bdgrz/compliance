using Bdgrz.Compliance.Features.Programs;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Policies;

[Discriminator("bdgrz.policy.versions.list", 1)]
public sealed record ListPolicyVersions(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<PolicyVersionView>>, IProgramReadRequest, ICallable;
