using Cntryl.Portia;

namespace Bdgrz.Compliance;

sealed class DeveloperRegistrationSessionBehavior(RegistrationSessionCookie session)
    : IRequestPipelineBehavior<RegisterDeveloperUser, RegisteredUserIdentity>
{
    public ValueTask<Result<RegisteredUserIdentity>> HandleAsync(
        IRequestContext<RegisterDeveloperUser> context,
        RequestPipelineNext<RegisteredUserIdentity> continuation,
        CancellationToken ct) => session.HandleAsync(continuation, ct);
}

sealed class OidcRegistrationSessionBehavior(RegistrationSessionCookie session)
    : IRequestPipelineBehavior<RegisterOidcUser, RegisteredUserIdentity>
{
    public ValueTask<Result<RegisteredUserIdentity>> HandleAsync(
        IRequestContext<RegisterOidcUser> context,
        RequestPipelineNext<RegisteredUserIdentity> continuation,
        CancellationToken ct) => session.HandleAsync(continuation, ct);
}

sealed class RegistrationSessionCookie(
    IHttpContextAccessor httpContextAccessor,
    BdgrzSessionTokens sessionTokens)
{
    public async ValueTask<Result<RegisteredUserIdentity>> HandleAsync(
        RequestPipelineNext<RegisteredUserIdentity> continuation,
        CancellationToken ct)
    {
        var result = await continuation(ct);
        var httpContext = httpContextAccessor.HttpContext;
        if (result.IsSuccess && httpContext is not null)
        {
            httpContext.Response.Cookies.Append(
                BdgrzSessionTokens.CookieName,
                sessionTokens.Issue(result.Value, DateTimeOffset.UtcNow),
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
