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

[Trait("Category", "WebIntegration")]
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
            ProductionEmailDeliveryTestConfiguration.Apply(builder);
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
        var sessionIdentity = new UserIdentity("https://prior-issuer.example/", "prior-subject");
        var secondSessionIdentity = new UserIdentity("https://prior-issuer.example/", "second-prior-subject");
        Assert.True(sessionIdentity.Register(firstUserId, null).IsSuccess);
        Assert.True(secondSessionIdentity.Register(secondUserId, null).IsSuccess);
        var writer = factory.Services.GetRequiredService<IAggregateWriter>();
        await writer.SaveAsync(sessionIdentity, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        await writer.SaveAsync(secondSessionIdentity, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var providerToken = Token("https://issuer.example/", "compliance-api",
            ProviderSecret, "provider-subject");

        // Act
        using var providerOnly = await PostLinkAsync(client, providerToken, null);
        var sessionToken = SessionToken(firstUserId, sessionIdentity.Id);
        using var sessionOnly = await PostLinkAsync(client, null, sessionToken);
        using var linked = await PostLinkAsync(client, providerToken, sessionToken);
        using var replayed = await PostLinkAsync(client, providerToken, sessionToken);
        using var taken = await PostLinkAsync(client, providerToken,
            SessionToken(secondUserId, secondSessionIdentity.Id));

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
    public async Task ShouldRejectExistingSessionGivenItsProviderIdentityWasRevoked()
    {
        // Arrange
        await using var factory = CreateExternalFactory();
        using var client = factory.CreateClient();
        var userId = Uuid.CreateVersion4();
        var retired = new UserIdentity("https://issuer.example/", "retired-subject");
        var replacement = new UserIdentity("https://issuer.example/", "replacement-subject");
        Assert.True(retired.Register(userId, null).IsSuccess);
        Assert.True(replacement.Register(userId, null).IsSuccess);
        var writer = factory.Services.GetRequiredService<IAggregateWriter>();
        await writer.SaveAsync(retired, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        await writer.SaveAsync(replacement, new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        var token = SessionToken(userId, retired.Id);
        using var before = await GetSessionAsync(client, token);
        retired = await factory.Services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new UserIdentity(retired.Id), CancellationToken.None);
        Assert.True(retired.Revoke(replacement.Id, DateTimeOffset.UtcNow).IsSuccess);
        await writer.SaveAsync(retired, new RequestDispatchContext(RequestActor.System), CancellationToken.None);

        // Act
        using var after = await GetSessionAsync(client, token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task ShouldRejectLegacySessionGivenIdentityBindingClaimIsMissing()
    {
        // Arrange
        await using var factory = CreateExternalFactory();
        using var client = factory.CreateClient();
        var legacyToken = Token("bdgrz", "bdgrz-browser", SessionSecret,
            Uuid.CreateVersion4().ToString());

        // Act
        using var response = await GetSessionAsync(client, legacyToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ShouldRequireProviderProofForRecoveryOptionsAndCompletionGivenAnonymousCaller()
    {
        // Arrange
        await using var factory = CreateExternalFactory();
        using var client = factory.CreateClient();

        // Act
        using var start = await client.PostAsJsonAsync(
            "/api/v1/identity-recovery/challenges",
            new RecoveryStartDocument("unknown@example.com"), CancellationToken.None);
        using var options = await client.PostAsJsonAsync(
            "/api/v1/identity-recovery/options",
            new RecoveryProofDocument("unknown@example.com", Uuid.CreateVersion4(), "proof"),
            CancellationToken.None);
        using var complete = await client.PostAsJsonAsync(
            "/api/v1/identity-recovery/completions",
            new RecoveryCompletionDocument("unknown@example.com", Uuid.CreateVersion4(),
                "proof", Uuid.CreateVersion4()), CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, start.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, options.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, complete.StatusCode);
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
            ProductionEmailDeliveryTestConfiguration.Apply(builder);
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

    static async Task<HttpResponseMessage> GetSessionAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/session");
        request.Headers.Add("Cookie", $"bdgrz_session={token}");
        return await client.SendAsync(request, CancellationToken.None);
    }

    static string SessionToken(Uuid userId, Uuid userIdentityId) => Token(
        "bdgrz", "bdgrz-browser", SessionSecret, userId.ToString(),
        [new Claim("user_identity_id", userIdentityId.ToString())]);

    static string Token(string issuer, string audience, string secret, string subject,
        Claim[]? additionalClaims = null)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(issuer, audience,
            [new Claim(JwtRegisteredClaimNames.Sub, subject), .. additionalClaims ?? []], now.AddMinutes(-1),
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

    sealed record RecoveryStartDocument(
        [property: JsonPropertyName("email_address")] string EmailAddress);

    sealed record RecoveryProofDocument(
        [property: JsonPropertyName("email_address")] string EmailAddress,
        [property: JsonPropertyName("challenge_id")] Uuid ChallengeId,
        [property: JsonPropertyName("token")] string Token);

    sealed record RecoveryCompletionDocument(
        [property: JsonPropertyName("email_address")] string EmailAddress,
        [property: JsonPropertyName("challenge_id")] Uuid ChallengeId,
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("old_identity_id")] Uuid OldIdentityId);

}
