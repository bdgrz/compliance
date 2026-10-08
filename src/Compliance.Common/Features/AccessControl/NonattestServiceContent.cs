using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed record NonattestServiceContent(Uuid ServiceEngagementId, string ServiceType,
    DateOnly StartedOn, DateOnly? EndedOn, IReadOnlyList<Uuid> FirmStaffMemberIds,
    bool InvolvedManagementFunctions, string SourceReference);
