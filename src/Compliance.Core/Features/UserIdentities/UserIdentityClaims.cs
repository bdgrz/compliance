using System.Globalization;
using System.Security.Claims;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

static class UserIdentityClaims
{
    public static bool TryGetBdgrzSubject(ClaimsPrincipal actor, out Uuid userId)
    {
        var identity = actor.Identities.FirstOrDefault(candidate =>
            candidate.IsAuthenticated &&
            string.Equals(candidate.FindFirst("iss")?.Value, "bdgrz", StringComparison.Ordinal));
        var value = identity?.FindFirst("sub")?.Value;
        return Uuid.TryParse(value, CultureInfo.InvariantCulture, out userId) && userId != Uuid.Empty;
    }
}
