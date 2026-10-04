using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>A recorded period of nonattest service provided by the firm to one client.</summary>
public sealed class NonattestServiceRecord
{
    public NonattestServiceRecord(Uuid clientTenantId, Uuid serviceEngagementId, string serviceType,
        DateOnly startedOn, DateOnly? endedOn, IReadOnlyList<Uuid> firmStaffMemberIds,
        bool involvedManagementFunctions)
    {
        ArgumentNullException.ThrowIfNull(firmStaffMemberIds);
        ClientTenantId = clientTenantId;
        ServiceEngagementId = serviceEngagementId;
        ServiceType = serviceType;
        StartedOn = startedOn;
        EndedOn = endedOn;
        FirmStaffMemberIds = Array.AsReadOnly(firmStaffMemberIds.ToArray());
        InvolvedManagementFunctions = involvedManagementFunctions;
    }

    public Uuid ClientTenantId { get; }
    public Uuid ServiceEngagementId { get; }
    public string ServiceType { get; }
    public DateOnly StartedOn { get; }
    public DateOnly? EndedOn { get; }
    public IReadOnlyList<Uuid> FirmStaffMemberIds { get; }
    public bool InvolvedManagementFunctions { get; }
}
