using System.Globalization;
using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

static class UserIdentityClaims
{
    static ClaimsIdentity? BdgrzIdentity(ClaimsPrincipal actor) =>
        actor.Identities.FirstOrDefault(candidate =>
            candidate.IsAuthenticated &&
            string.Equals(candidate.FindFirst("iss")?.Value, "bdgrz", StringComparison.Ordinal));

    public static bool TryGetBdgrzSubject(ClaimsPrincipal actor, out Uuid userId)
    {
        var value = BdgrzIdentity(actor)?.FindFirst("sub")?.Value;
        return Uuid.TryParse(value, CultureInfo.InvariantCulture, out userId) && userId != Uuid.Empty;
    }

    public static string BdgrzDisplay(ClaimsPrincipal actor, Uuid userId)
    {
        var email = BdgrzIdentity(actor)?.FindFirst("email")?.Value;
        return string.IsNullOrWhiteSpace(email) ? userId.ToString() : email;
    }
}
