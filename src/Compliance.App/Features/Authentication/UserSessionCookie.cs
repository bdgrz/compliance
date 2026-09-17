using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Authentication;

/// <summary>Issues the Bdgrz browser session cookie for a successful identity continuation.</summary>
static class UserSessionCookie
{
    public static IResult? Issue(
        HttpContext http,
        Result<AuthenticatedUserIdentity> result,
        BdgrzSessionTokens sessionTokens)
    {
        if (result.IsSuccess)
        {
            http.Response.Cookies.Append(
                BdgrzSessionTokens.CookieName,
                sessionTokens.Issue(result.Value.UserId, result.Value.EmailAddress, DateTimeOffset.UtcNow),
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    MaxAge = TimeSpan.FromHours(12),
                    SameSite = SameSiteMode.Lax,
                    Secure = http.Request.IsHttps,
                });
        }

        return null;
    }
}
