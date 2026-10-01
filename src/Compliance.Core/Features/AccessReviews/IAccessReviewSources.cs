using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     The governed workforce and inventory facts access reviews correlate with. Each read fails
///     transiently rather than answering from a projection that has not reached its source.
/// </summary>
public interface IAccessReviewSources
{
    /// <summary>Lists the tenant's roster people.</summary>
    ValueTask<Result<IReadOnlyList<PersonView>>> ListPeopleAsync(Uuid tenantId,
        CancellationToken ct = default);

    /// <summary>Lists the lifecycle statuses of a person's work relationships.</summary>
    ValueTask<Result<IReadOnlyList<string>>> RelationshipStatusesAsync(Uuid tenantId,
        Uuid personId, CancellationToken ct = default);

    /// <summary>Reads a service identity with its ownership and expiry evaluated now.</summary>
    ValueTask<Result<ServiceIdentityView>> GetServiceIdentityAsync(Uuid tenantId,
        Uuid serviceIdentityId, CancellationToken ct = default);

    /// <summary>The member correlated with the application's access owner, if any.</summary>
    ValueTask<Result<Uuid?>> AccessOwnerMemberAsync(Uuid tenantId, Uuid applicationId,
        CancellationToken ct = default);
}
