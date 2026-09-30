using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>Checks that a relationship's person, manager, and sponsor are recorded people of the tenant.</summary>
static class WorkRelationshipReferences
{
    public static async ValueTask<RequestError?> ValidateAsync(IAggregateReader reader,
        Uuid tenantId, Uuid personId, WorkRelationshipTerms terms, CancellationToken ct)
    {
        foreach (var (id, role) in new (Uuid? Id, string Role)[]
                 {
                     (personId, "person"), (terms.ManagerPersonId, "manager"),
                     (terms.SponsorPersonId, "sponsor"),
                 })
        {
            if (id is not { } value)
                continue;
            var person = await reader.HydrateAsync(new Person(tenantId, value), ct)
                .ConfigureAwait(false);
            if (!person.IsCreated)
                return new RequestError(RequestErrorKind.Validation,
                    $"The work relationship {role} must be a recorded person of this organization.");
        }
        return null;
    }
}
