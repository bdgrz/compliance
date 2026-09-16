using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

sealed class UserSessionCookie(
    IHttpContextAccessor httpContextAccessor,
    BdgrzSessionTokens sessionTokens)
{
    public ValueTask<Result<AuthenticatedUserIdentity>> HandleAsync(
        RequestPipelineNext<AuthenticatedUserIdentity> continuation,
        CancellationToken ct) => HandleAsync(continuation, static result => result.Value.UserId,
        static result => result.Value.EmailAddress, ct);

    async ValueTask<Result<T>> HandleAsync<T>(
        RequestPipelineNext<T> continuation,
        Func<Result<T>, Uuid> userId,
        Func<Result<T>, string?> emailAddress,
        CancellationToken ct)
    {
        var result = await continuation(ct);
        var httpContext = httpContextAccessor.HttpContext;
        if (result.IsSuccess && httpContext is not null)
        {
            httpContext.Response.Cookies.Append(
                BdgrzSessionTokens.CookieName,
                sessionTokens.Issue(userId(result), emailAddress(result), DateTimeOffset.UtcNow),
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    MaxAge = TimeSpan.FromHours(12),
                    SameSite = SameSiteMode.Lax,
                    Secure = httpContext.Request.IsHttps,
                });
        }

        return result;
    }
}
