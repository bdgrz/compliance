using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.UserIdentities;
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
public sealed class IdentityRecoveryE2ETests(BrokerStackFixture broker) : IClassFixture<BrokerStackFixture>
{
    const string SessionSecret = "bdgrz-recovery-e2e-session-signing-key-01";
    const string ProviderSecret = "bdgrz-recovery-e2e-provider-signing-key-01";
    const string BootstrapOperatorId = "71148ac3-3488-4895-b706-85749f260e47";

    [Fact]
    public async Task ShouldReplaceIdentityAcrossApiAndWorkerGivenVerifiedRecoveryEmail()
    {
        // Arrange
        var applicationName = $"compliance-recovery-split-{Guid.NewGuid():N}";
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var delivery = new RecordingDelivery();
        using var worker = CreateWorker(applicationName, key, delivery);
        await worker.StartAsync();
        await using var factory = CreateExternalFactory(applicationName, key);
        var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
        HttpClient client;
        try
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
            client = factory.CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
        }

        using var owner = client;
        const string email = "recovery@example.com";
        var userId = Uuid.CreateVersion4();
        var oldIdentity = new UserIdentity("https://issuer.example/", "old-subject");
        var foreignIdentity = new UserIdentity("https://issuer.example/", "foreign-subject");
        using var scope = factory.Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        Assert.True(oldIdentity.Register(userId, email).IsSuccess);
        Assert.True(foreignIdentity.Register(Uuid.CreateVersion4(), null).IsSuccess);
        await writer.SaveAsync(oldIdentity, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        await writer.SaveAsync(foreignIdentity, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        var now = DateTimeOffset.UtcNow;
        const string verificationToken = "previously-verified-email-proof";
        var address = new EmailAddress(email);
        Assert.True(address.Reserve(userId).IsSuccess);
        Assert.True(address.IssueChallenge(userId, Uuid.CreateVersion4(),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(verificationToken))),
            now.AddMinutes(15), now).IsSuccess);
        Assert.True(address.CompleteChallenge(userId, verificationToken, now).IsSuccess);
        await writer.SaveAsync(address, new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);

        // Act: the API writes the challenge; the independent worker delivers it.
        using var started = await owner.PostAsJsonAsync("/api/v1/identity-recovery/challenges",
            new { email_address = email });
        Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);
        var challenge = await WaitForRecoveryAsync(delivery);
        var providerToken = Token("https://issuer.example/", "compliance-api",
            ProviderSecret, "new-subject");
        using var optionsRequest = new HttpRequestMessage(HttpMethod.Post,
            "/api/v1/identity-recovery/options")
        {
            Content = JsonContent.Create(new
            {
                email_address = email,
                challenge_id = challenge.ChallengeId,
                token = challenge.Token,
            }),
        };
        optionsRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", providerToken);
        using var options = await SendUntilSuccessAsync(owner, optionsRequest,
            [HttpStatusCode.Conflict]);
        var optionsBody = await options.Content.ReadFromJsonAsync<RecoveryOptionsDocument>();
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        Assert.True(optionsBody!.ProjectionRevision > 0);
        Assert.Equal(oldIdentity.Id.ToString(), Assert.Single(optionsBody.Identities.Items).UserIdentityId);

        // Preserve a live old-identity session so revocation is proven through authentication.
        var oldProviderToken = Token("https://issuer.example/", "compliance-api",
            ProviderSecret, "old-subject");
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/oidc-user-sessions")
        {
            Content = JsonContent.Create(new { }),
        };
        loginRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", oldProviderToken);
        using var login = await owner.SendAsync(loginRequest);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var sessionCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"))
            .Split(';', 2)[0];
        using var beforeRevocation = await GetSessionAsync(owner, sessionCookie);
        Assert.Equal(HttpStatusCode.OK, beforeRevocation.StatusCode);

        using var completionRequest = new HttpRequestMessage(HttpMethod.Post,
            "/api/v1/identity-recovery/completions")
        {
            Content = JsonContent.Create(new
            {
                email_address = email,
                challenge_id = challenge.ChallengeId,
                token = challenge.Token,
                old_identity_id = oldIdentity.Id,
            }),
        };
        completionRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", providerToken);
        using var completed = await owner.SendAsync(completionRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        using var afterRevocation = await GetSessionAsync(owner, sessionCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevocation.StatusCode);
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var replacement = await reader.HydrateAsync(
            new UserIdentity("https://issuer.example/", "new-subject"), CancellationToken.None);
        var retired = await reader.HydrateAsync(new UserIdentity(oldIdentity.Id), CancellationToken.None);
        Assert.Equal(userId, replacement.UserId);
        Assert.True(retired.IsRevoked);
        await WaitForCompletionNoticeAsync(delivery, challenge.ChallengeId);
        await worker.StopAsync();
    }

    IHost CreateWorker(string applicationName, string key, RecordingDelivery delivery)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Configuration["Compliance:EmailDelivery:ActiveTokenKeyId"] = "test-key";
        builder.Configuration["Compliance:EmailDelivery:TokenKeys:test-key"] = key;
        builder.Configuration["PlatformOperators:UserIds:0"] = BootstrapOperatorId;
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: false)
            .AddWorkers();
        builder.Services.AddSingleton<IEmailChallengeDelivery>(delivery);
        return builder.Build();
    }

    WebApplicationFactory<Program> CreateExternalFactory(string applicationName, string key) =>
        E2EAppFactory.Create(broker, applicationName).WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Compliance:Authentication:Mode", "External");
            builder.UseSetting("Compliance:Authentication:Authority", "https://issuer.example/");
            builder.UseSetting("Compliance:Authentication:Audience", "compliance-api");
            builder.UseSetting("Compliance:Authentication:ClientId", "compliance-spa");
            builder.UseSetting("BDGRZ_SESSION_SIGNING_KEY", SessionSecret);
            builder.UseSetting("PlatformOperators:UserIds:0", BootstrapOperatorId);
            builder.UseSetting("Compliance:EmailDelivery:ActiveTokenKeyId", "test-key");
            builder.UseSetting("Compliance:EmailDelivery:TokenKeys:test-key", key);
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

    static async Task<RecoveryDelivery> WaitForRecoveryAsync(RecordingDelivery delivery)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (delivery.Recovery.TryPeek(out var challenge))
                return challenge;
            await Task.Delay(100);
        }
        Assert.Fail("Identity recovery email was not delivered by the independent worker.");
        throw new InvalidOperationException("Unreachable.");
    }

    static async Task WaitForCompletionNoticeAsync(RecordingDelivery delivery, Uuid challengeId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (delivery.Completions.Contains(challengeId))
                return;
            await Task.Delay(100);
        }
        Assert.Fail("Identity recovery completion notice was not sent by the independent worker.");
    }

    static async Task<HttpResponseMessage> SendUntilSuccessAsync(HttpClient client,
        HttpRequestMessage request, IReadOnlyCollection<HttpStatusCode> retryStatuses)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            using var clone = await CloneAsync(request);
            var response = await client.SendAsync(clone);
            if (!retryStatuses.Contains(response.StatusCode) || DateTimeOffset.UtcNow >= deadline)
                return response;
            response.Dispose();
            await Task.Delay(100);
        }
    }

    static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage source)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Content = source.Content is null
                ? null
                : new StringContent(await source.Content.ReadAsStringAsync(), Encoding.UTF8,
                    source.Content.Headers.ContentType?.MediaType ?? "application/json"),
        };
        foreach (var header in source.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }

    static async Task<HttpResponseMessage> GetSessionAsync(HttpClient client, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/session");
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    static string Token(string issuer, string audience, string secret, string subject)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(issuer, audience,
            [new Claim(JwtRegisteredClaimNames.Sub, subject), new Claim(JwtRegisteredClaimNames.Email,
                "recovery@example.com")], now.AddMinutes(-1), now.AddMinutes(30),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    sealed class RecordingDelivery : IEmailChallengeDelivery
    {
        public ConcurrentQueue<RecoveryDelivery> Recovery { get; } = new();
        public ConcurrentQueue<Uuid> Completions { get; } = new();

        public ValueTask SendAsync(Uuid challengeId, Uuid userId, string emailAddress,
            string token, CancellationToken ct)
        {
            _ = challengeId;
            _ = userId;
            _ = emailAddress;
            _ = token;
            ct.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public ValueTask SendRecoveryAsync(Uuid challengeId, Uuid userId, string emailAddress,
            string token, CancellationToken ct)
        {
            _ = userId;
            _ = emailAddress;
            ct.ThrowIfCancellationRequested();
            Recovery.Enqueue(new RecoveryDelivery(challengeId, token));
            return ValueTask.CompletedTask;
        }

        public ValueTask SendRecoveryCompletedAsync(Uuid challengeId, Uuid userId,
            string emailAddress, CancellationToken ct)
        {
            _ = userId;
            _ = emailAddress;
            ct.ThrowIfCancellationRequested();
            Completions.Enqueue(challengeId);
            return ValueTask.CompletedTask;
        }
    }

    sealed record RecoveryDelivery(Uuid ChallengeId, string Token);
    sealed record RecoveryOptionsDocument(
        [property: JsonPropertyName("projection_revision")] long ProjectionRevision,
        RecoveryPageDocument Identities);
    sealed record RecoveryPageDocument(IReadOnlyList<RecoveryOptionDocument> Items);
    sealed record RecoveryOptionDocument(
        [property: JsonPropertyName("user_identity_id")] string UserIdentityId,
        string Provider);
}
