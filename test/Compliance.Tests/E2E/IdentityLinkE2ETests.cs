using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class IdentityLinkE2ETests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    const string SessionSecret = "bdgrz-link-e2e-session-signing-key-0001";
    const string ProviderSecret = "bdgrz-link-e2e-provider-signing-key-001";

    [Fact]
    public async Task ShouldPreserveLinkedUserGivenIndependentApiAndWorker()
    {
        // Arrange
        var applicationName = $"compliance-link-split-{Guid.NewGuid():N}";
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        using var worker = builder.Build();
        await worker.StartAsync();

        try
        {
            var userId = Uuid.CreateVersion4();
            var providerToken = Token("https://issuer.example/", "compliance-api",
                ProviderSecret, "split-provider-subject");
            await using (var firstFactory = CreateExternalFactory(applicationName))
            {
                using var firstClient = firstFactory.CreateClient();
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    "/api/v1/my/oidc_identity_links")
                {
                    Content = JsonContent.Create(new { }),
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", providerToken);
                request.Headers.Add("Cookie", "bdgrz_session=" + Token("bdgrz",
                    "bdgrz-browser", SessionSecret, userId.ToString()));

                // Act
                using var linked = await firstClient.SendAsync(request, CancellationToken.None);

                // Assert
                Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
                var result = await linked.Content.ReadFromJsonAsync<LinkedIdentityDocument>();
                Assert.Equal(userId.ToString(), result?.UserId);
            }

            await using var secondFactory = CreateExternalFactory(applicationName);
            using var secondClient = secondFactory.CreateClient();
            using var continuationRequest = new HttpRequestMessage(HttpMethod.Post,
                "/api/v1/oidc-user-sessions")
            {
                Content = JsonContent.Create(new { }),
            };
            continuationRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", providerToken);

            // Act
            using var continued = await secondClient.SendAsync(continuationRequest,
                CancellationToken.None);

            // Assert
            Assert.Equal(HttpStatusCode.OK, continued.StatusCode);
            var authenticated = await continued.Content
                .ReadFromJsonAsync<LinkedIdentityDocument>();
            Assert.Equal(userId.ToString(), authenticated?.UserId);
        }
        finally
        {
            await worker.StopAsync();
        }
    }

    WebApplicationFactory<Program> CreateExternalFactory(string applicationName) =>
        E2EAppFactory.Create(broker, applicationName).WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "false");
            builder.UseSetting("Compliance:Authentication:Mode", "External");
            builder.UseSetting("Compliance:Authentication:Authority", "https://issuer.example/");
            builder.UseSetting("Compliance:Authentication:Audience", "compliance-api");
            builder.UseSetting("Compliance:Authentication:ClientId", "compliance-spa");
            builder.UseSetting("BDGRZ_SESSION_SIGNING_KEY", SessionSecret);
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<JwtBearerOptions>("BdgrzResource0", options =>
                {
                    options.Configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = "https://issuer.example/",
                    };
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(ProviderSecret));
                    options.TokenValidationParameters.ValidIssuer = "https://issuer.example/";
                    options.TokenValidationParameters.ValidAudience = "compliance-api";
                });
            });
        });

    static string Token(string issuer, string audience, string secret, string subject)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(issuer, audience,
            [new Claim(JwtRegisteredClaimNames.Sub, subject)], now.AddMinutes(-1),
            now.AddMinutes(30), new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    sealed record LinkedIdentityDocument(
        [property: JsonPropertyName("user_id")] string UserId);
}
