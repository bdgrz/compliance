using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.UserIdentities;

public sealed class UserIdentityContinuationWebTests
{
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
            });
        });

    sealed record RegistrationDocument(
        [property: JsonPropertyName("email_address")] string EmailAddress);

    sealed record SessionDocument(
        string Id,
        [property: JsonPropertyName("email_address")] string EmailAddress,
        [property: JsonPropertyName("email_address_verified")]
        bool EmailAddressVerified);
}
