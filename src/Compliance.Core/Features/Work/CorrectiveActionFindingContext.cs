using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record CorrectiveActionFindingContext(Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    string Title, string Severity, long Revision);
