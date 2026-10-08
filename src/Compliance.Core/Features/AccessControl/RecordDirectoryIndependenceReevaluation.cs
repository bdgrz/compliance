using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Internal source-verifying effect; neither a public request nor acceptance authority.</summary>
[Discriminator("bdgrz.independence.record-directory-reevaluation", 1)]
sealed record RecordDirectoryIndependenceReevaluation(Uuid TenantId,
    ulong DirectoryResourceOffset, Uuid DirectoryEventId, string DirectoryPayloadSha256,
    ulong AcceptanceResourceOffset, Uuid AcceptanceEventId, Uuid AcceptanceRequestId,
    string AcceptancePayloadSha256, Uuid StaffMemberId, Uuid UserId) : IRequest;
