using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Responsibilities;

public interface IMemberResponsibilityIndex
{
    ValueTask<IReadOnlyList<ResponsibilityAssignmentView>> GetAsync(Uuid tenantId,
        Uuid memberId, CancellationToken ct = default);
}
