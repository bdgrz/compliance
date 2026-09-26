using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Checks that named application owners are workforce people in the same tenant.</summary>
static class ApplicationOwnerReferences
{
    public static async ValueTask<RequestError?> ValidateAsync(IAggregateReader reader,
        Uuid tenantId, Uuid? systemOwnerPersonId, Uuid? accessOwnerPersonId,
        CancellationToken ct)
    {
        if (!await ExistsAsync(reader, tenantId, systemOwnerPersonId, ct).ConfigureAwait(false))
            return new RequestError(RequestErrorKind.Validation,
                "The system owner must be a recorded workforce person in this tenant.");
        if (!await ExistsAsync(reader, tenantId, accessOwnerPersonId, ct).ConfigureAwait(false))
            return new RequestError(RequestErrorKind.Validation,
                "The access owner must be a recorded workforce person in this tenant.");
        return null;
    }

    static async ValueTask<bool> ExistsAsync(IAggregateReader reader, Uuid tenantId,
        Uuid? personId, CancellationToken ct) =>
        personId is not { } id ||
        (await reader.HydrateAsync(new Person(tenantId, id), ct).ConfigureAwait(false)).IsCreated;
}
