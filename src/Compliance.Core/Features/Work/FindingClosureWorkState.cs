using Bdgrz.Compliance.Features.Remediation;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Work;

public sealed record FindingClosureWorkState(Uuid TenantId, Uuid ProgramId, Uuid FindingId,
    long Revision, string Title, string Severity, Uuid OwnerMemberId, DateOnly DueOn,
    CorrectiveActionView[] Actions, bool Closed, DateTimeOffset ChangedAt);
