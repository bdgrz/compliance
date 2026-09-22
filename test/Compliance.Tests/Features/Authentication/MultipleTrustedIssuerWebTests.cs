using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Bdgrz.Compliance.Tests.Features.Authentication;

/// <summary>ADR 0009: each trusted OIDC issuer is validated directly with its own keys.</summary>
public sealed class MultipleTrustedIssuerWebTests
{
    const string FirmIssuer = "https://firm-issuer.example/";
    const string ClientIssuer = "https://client-issuer.example/";
    const string FirmSecret = "firm-issuer-test-signing-key-00000001";
    const string ClientSecret = "client-issuer-test-signing-key-0000001";

    [Fact]
    public async Task ShouldBindSeparateUsersGivenSameSubjectFromTwoTrustedIssuers()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        using var firm = await ContinueAsync(client, Token(FirmIssuer, FirmSecret, "shared-subject"));
        using var clientIssuer = await ContinueAsync(client,
            Token(ClientIssuer, ClientSecret, "shared-subject"));
        using var firmAgain = await ContinueAsync(client, Token(FirmIssuer, FirmSecret, "shared-subject"));

        // Assert
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK),
            (firm.StatusCode, clientIssuer.StatusCode, firmAgain.StatusCode));
        var firmUser = await firm.Content.ReadFromJsonAsync<IdentityDocument>();
        var clientUser = await clientIssuer.Content.ReadFromJsonAsync<IdentityDocument>();
        var firmReplay = await firmAgain.Content.ReadFromJsonAsync<IdentityDocument>();
        Assert.NotNull(firmUser);
        Assert.NotNull(clientUser);
        Assert.NotEqual(firmUser.UserId, clientUser.UserId);
        Assert.Equal(firmUser.UserId, firmReplay?.UserId);
    }

    [Fact]
    public async Task ShouldRejectTokenGivenIssuerSignedWithAnotherIssuersKey()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        using var crossSigned = await ContinueAsync(client, Token(ClientIssuer, FirmSecret, "subject"));
        using var untrusted = await ContinueAsync(client,
            Token("https://untrusted-issuer.example/", FirmSecret, "subject"));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, crossSigned.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, untrusted.StatusCode);
    }

    static async Task<HttpResponseMessage> ContinueAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/oidc-user-sessions")
        {
            Content = JsonContent.Create(new { }),
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, CancellationToken.None);
    }

    static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Compliance:Authentication:Mode", "External");
            builder.UseSetting("Compliance:Authentication:Authority", FirmIssuer);
            builder.UseSetting("Compliance:Authentication:ClientId", "compliance-spa");
            // Resource schemes are numbered in configuration-key order.
            builder.UseSetting("Compliance:Authentication:Resources:A-Firm:Audience", "compliance-api");
            builder.UseSetting("Compliance:Authentication:Resources:B-Client:Authority", ClientIssuer);
            builder.UseSetting("Compliance:Authentication:Resources:B-Client:Audience", "compliance-api");
            builder.UseSetting("BDGRZ_SESSION_SIGNING_KEY", "bdgrz-test-session-signing-key-000001");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "compliance-multi-issuer-tests");
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(item =>
                             item.ServiceType == typeof(IHostedService)).ToArray())
                    services.Remove(descriptor);
                services.RemoveAll<IEventStore>();
                services.AddSingleton<IEventStore, InMemoryEventStore>();
                TrustIssuer(services, "BdgrzResource0", FirmIssuer, FirmSecret);
                TrustIssuer(services, "BdgrzResource1", ClientIssuer, ClientSecret);
            });
        });

    static void TrustIssuer(IServiceCollection services, string scheme, string issuer, string secret) =>
        services.PostConfigure<JwtBearerOptions>(scheme, options =>
        {
            // Replaces metadata discovery with the issuer's static test key.
            options.Configuration = new OpenIdConnectConfiguration { Issuer = issuer };
            options.Configuration.SigningKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)));
            options.TokenValidationParameters.IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            options.TokenValidationParameters.ValidIssuer = issuer;
            options.TokenValidationParameters.ValidAudience = "compliance-api";
        });

    static string Token(string issuer, string secret, string subject)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(issuer, "compliance-api",
            [new Claim(JwtRegisteredClaimNames.Sub, subject)], now.AddMinutes(-1),
            now.AddMinutes(30), new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    sealed record IdentityDocument(
        [property: JsonPropertyName("user_identity_id")] string UserIdentityId,
        [property: JsonPropertyName("user_id")] string UserId);
}
