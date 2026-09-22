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

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class UserIdentityContinuationWebTests
{
    const string SessionSecret = "bdgrz-test-session-signing-key-000001";
    const string ProviderSecret = "oidc-provider-test-signing-key-000001";

    [Fact]
    public async Task ShouldRequireAuthenticationGivenOidcContinuation()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Compliance:Authentication:Mode", "External");
            builder.UseSetting("Compliance:Authentication:Authority", "https://issuer.example/");
            builder.UseSetting("Compliance:Authentication:Audience", "compliance-api");
            builder.UseSetting("Compliance:Authentication:ClientId", "compliance-spa");
            builder.UseSetting("BDGRZ_SESSION_SIGNING_KEY", "bdgrz-test-session-signing-key-000001");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "compliance-registration-tests");
            builder.ConfigureServices(services =>
            {
                var hostedServices = services
                    .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                    .ToArray();
                foreach (var descriptor in hostedServices)
                {
                    services.Remove(descriptor);
                }
            });
        });
        using var client = factory.CreateClient();

        // Act
        using var response = await client.PostAsJsonAsync(
            "/api/v1/oidc-user-sessions",
            new RegistrationDocument("person@example.com"),
            CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldLinkOnlyWithSessionAndProviderProofGivenExternalAuthentication()
    {
        // Arrange
        await using var factory = CreateExternalFactory();
        using var client = factory.CreateClient();
        var firstUserId = Uuid.CreateVersion4();
        var secondUserId = Uuid.CreateVersion4();
        var providerToken = Token("https://issuer.example/", "compliance-api",
            ProviderSecret, "provider-subject");

        // Act
        using var providerOnly = await PostLinkAsync(client, providerToken, null);
        using var sessionOnly = await PostLinkAsync(client, null,
            Token("bdgrz", "bdgrz-browser", SessionSecret, firstUserId.ToString()));
        using var linked = await PostLinkAsync(client, providerToken,
            Token("bdgrz", "bdgrz-browser", SessionSecret, firstUserId.ToString()));
        using var replayed = await PostLinkAsync(client, providerToken,
            Token("bdgrz", "bdgrz-browser", SessionSecret, firstUserId.ToString()));
        using var taken = await PostLinkAsync(client, providerToken,
            Token("bdgrz", "bdgrz-browser", SessionSecret, secondUserId.ToString()));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, providerOnly.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, sessionOnly.StatusCode);
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        var link = await linked.Content.ReadFromJsonAsync<LinkedIdentityDocument>();
        var replay = await replayed.Content.ReadFromJsonAsync<LinkedIdentityDocument>();
        Assert.Equal(firstUserId.ToString(), link?.UserId);
        Assert.Equal(link, replay);
    }

    [Fact]
    public async Task ShouldContinueAndEstablishSessionGivenNewDevelopmentIdentity()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var submission = new RegistrationDocument("  Person@Example.COM  ");

        // Act
        using var response = await client.PostAsJsonAsync(
            "/api/v1/developer-user-sessions",
            submission,
            CancellationToken.None);
        using var sessionResponse = await client.GetAsync("/auth/session", CancellationToken.None);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionDocument>(CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("bdgrz_session=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.Equal("person@example.com", session?.EmailAddress);
        Assert.False(session?.EmailAddressVerified);
    }

    [Fact]
    public async Task ShouldAuthenticateGivenExistingEquivalentDevelopmentIdentity()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        // Act
        using var first = await firstClient.PostAsJsonAsync(
            "/api/v1/developer-user-sessions",
            new RegistrationDocument("person@example.com"),
            CancellationToken.None);
        using var second = await secondClient.PostAsJsonAsync(
            "/api/v1/developer-user-sessions",
            new RegistrationDocument(" PERSON@example.com "),
            CancellationToken.None);
        using var firstSessionResponse = await firstClient.GetAsync("/auth/session", CancellationToken.None);
        using var secondSessionResponse = await secondClient.GetAsync("/auth/session", CancellationToken.None);
        var firstSession = await firstSessionResponse.Content.ReadFromJsonAsync<SessionDocument>(CancellationToken.None);
        var secondSession = await secondSessionResponse.Content.ReadFromJsonAsync<SessionDocument>(CancellationToken.None);
        var events = new List<DomainEvent>();
        var store = factory.Services.GetRequiredService<IEventStore>();
        await foreach (var record in store.ReadAsync(
                           EventStreamPattern.ForPattern("bdgrz", "user-identities"),
                           EventCursor.Start,
                           CancellationToken.None))
        {
            events.Add(record.Event);
        }

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstSession?.Id, secondSession?.Id);
        Assert.Equal(
            ["UserIdentityRegistered", "UserIdentityAuthenticated"],
            events.Select(item => item.GetType().Name));
        Assert.False(events[0].Metadata.IsAudit);
        Assert.True(events[1].Metadata.IsAudit);
    }

    [Fact]
    public async Task ShouldEndTheSessionGivenLogout()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync(
            "/api/v1/developer-user-sessions",
            new RegistrationDocument("person@example.com"),
            CancellationToken.None);

        // Act
        using var logout = await client.PostAsync("/auth/logout", null, CancellationToken.None);
        using var sessionAfterLogout = await client.GetAsync("/auth/session", CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var cookie = Assert.Single(logout.Headers.GetValues("Set-Cookie"));
        Assert.Contains("bdgrz_session=", cookie, StringComparison.Ordinal);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, sessionAfterLogout.StatusCode);
    }

    [Fact]
    public async Task ShouldRejectInvalidEmailGivenDeveloperIdentity()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        using var invalid = await client.PostAsJsonAsync(
            "/api/v1/developer-user-sessions",
            new RegistrationDocument("not-an-email"),
            CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BDGRZ_DEVELOPER_AUTH", "true");
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "compliance-registration-tests");
            builder.ConfigureServices(services =>
            {
                var brokerHostedServices = services
                    .Where(descriptor =>
                        descriptor.ServiceType == typeof(IHostedService) &&
                        descriptor.ImplementationType?.Name != "ComplianceReadinessLifecycle")
                    .ToArray();
                foreach (var descriptor in brokerHostedServices)
                {
                    services.Remove(descriptor);
                }

                services.RemoveAll<IEventStore>();
                services.AddSingleton<IEventStore, InMemoryEventStore>();
                services.RemoveAll<IEmailAddressDirectoryReader>();
                services.AddSingleton<IEmailAddressDirectoryReader, EmptyEmailAddressDirectory>();
            });
        });

    static WebApplicationFactory<Program> CreateExternalFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Compliance:Authentication:Mode", "External");
            builder.UseSetting("Compliance:Authentication:Authority", "https://issuer.example/");
            builder.UseSetting("Compliance:Authentication:Audience", "compliance-api");
            builder.UseSetting("Compliance:Authentication:ClientId", "compliance-spa");
            builder.UseSetting("BDGRZ_SESSION_SIGNING_KEY", SessionSecret);
            builder.UseSetting("Fitz:Endpoint", "ws://127.0.0.1:4090/ws");
            builder.UseSetting("Fitz:ApplicationName", "compliance-link-tests");
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(item =>
                             item.ServiceType == typeof(IHostedService)).ToArray())
                    services.Remove(descriptor);
                services.RemoveAll<IEventStore>();
                services.AddSingleton<IEventStore, InMemoryEventStore>();
                services.PostConfigure<JwtBearerOptions>("BdgrzResource0", options =>
                {
                    options.Configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = "https://issuer.example/",
                    };
                    options.Configuration.SigningKeys.Add(new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(ProviderSecret)));
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(ProviderSecret));
                    options.TokenValidationParameters.ValidIssuer = "https://issuer.example/";
                    options.TokenValidationParameters.ValidAudience = "compliance-api";
                });
            });
        });

    static async Task<HttpResponseMessage> PostLinkAsync(HttpClient client,
        string? providerToken, string? sessionToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "/api/v1/my/oidc-identity-links")
        {
            Content = JsonContent.Create(new { }),
        };
        if (providerToken is not null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", providerToken);
        if (sessionToken is not null)
            request.Headers.Add("Cookie", $"bdgrz_session={sessionToken}");
        return await client.SendAsync(request, CancellationToken.None);
    }

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

    sealed class EmptyEmailAddressDirectory : IEmailAddressDirectoryReader
    {
        public ValueTask<EmailAddressView?> GetAsync(string emailAddress, CancellationToken ct = default) =>
            ValueTask.FromResult<EmailAddressView?>(null);

        public ValueTask<Page<EmailAddressView>> ListAsync(Uuid userId, int? limit, string? cursor,
            CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<EmailAddressView>([], null));
    }

    sealed record RegistrationDocument(
        [property: JsonPropertyName("email_address")] string EmailAddress);

    sealed record SessionDocument(
        string Id,
        [property: JsonPropertyName("email_address")] string EmailAddress,
        [property: JsonPropertyName("email_address_verified")]
        bool EmailAddressVerified);

    sealed record LinkedIdentityDocument(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("user_identity_id")] string UserIdentityId);
}
