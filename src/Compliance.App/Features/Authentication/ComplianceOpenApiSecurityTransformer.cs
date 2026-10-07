using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bdgrz.Compliance.Features.Authentication;

/// <summary>Describes the existing API-user authentication contract without changing runtime policy.</summary>
public sealed class ComplianceOpenApiSecurityTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    const string CookieScheme = "BdgrzSessionCookie";
    const string BearerScheme = "BdgrzBearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[CookieScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = BdgrzSessionTokens.CookieName,
            Description = "Bdgrz browser session. Actor, tenant membership and operation permissions are checked separately.",
        };
        document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "A Bdgrz session JWT or access token validated by this deployment's configured issuer and audience. Actor and operation permissions are checked separately.",
        };
        return Task.CompletedTask;
    }

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata is null || metadata.OfType<IAllowAnonymous>().Any() ||
            !metadata.OfType<IAuthorizeData>().Any(value => value.Policy == ComplianceAuthorizationPolicies.ApiUser) ||
            operation.Security is { Count: > 0 })
            return Task.CompletedTask;
        operation.Security =
        [
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(CookieScheme, context.Document)] = [] },
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [] },
        ];
        return Task.CompletedTask;
    }
}
