using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Checks that a service identity's owner is a recorded person or an active team.</summary>
static class ServiceIdentityOwners
{
    public static async ValueTask<RequestError?> ValidateAsync(IAggregateReader reader,
        Uuid tenantId, string ownerKind, Uuid ownerId, CancellationToken ct)
    {
        var exists = ownerKind switch
        {
            "person" => (await reader.HydrateAsync(new Person(tenantId, ownerId), ct)
                .ConfigureAwait(false)).IsCreated,
            "team" => (await reader.HydrateAsync(new Team(tenantId, ownerId), ct)
                .ConfigureAwait(false)).IsActive,
            _ => true,
        };
        return exists
            ? null
            : new RequestError(RequestErrorKind.Validation,
                "The service identity owner must be a recorded person or an active team of this organization.");
    }
}
