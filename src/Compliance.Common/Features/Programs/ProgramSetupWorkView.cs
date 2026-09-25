using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed record ProgramSetupWorkView(Uuid TenantId, Uuid ProgramId,
    long ProgramRevision, IReadOnlyList<ProgramSetupWorkItem> Items,
    string? NextBoundaryCursor);
