using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class SplitHostTenantE2ETests(BrokerStackFixture broker)
{
    [Fact]
    public async Task ShouldRetryDirectInvitationGivenDeliveryFailure()
    {
        // Arrange
        var applicationName = $"compliance-split-direct-retry-{Guid.NewGuid():N}";
        var delivery = new FailingOnceInvitationDelivery();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        using var worker = builder.Build();

        // Act
        await worker.StartAsync();

        // Assert
        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName)
                .WithWebHostBuilder(host => host.ConfigureTestServices(services =>
                    services.AddSingleton<ITenantInvitationDelivery>(delivery)));
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
            using var operatorClient = client;
            await TenantInvitationE2ETests.LoginAsync(operatorClient,
                $"operator-{Guid.NewGuid():N}@example.com");
            var administratorEmail = $"administrator-{Guid.NewGuid():N}@example.com";
            using var registered = await operatorClient.PostAsJsonAsync("/api/v1/tenants", new
            {
                name = "Direct Invitation Retry",
                legal_name = "Direct Invitation Retry LLC",
                slug = $"direct-retry-{Guid.NewGuid():N}"[..24],
                first_administrator_email = administratorEmail,
            });
            Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
            var registration = await registered.Content.ReadFromJsonAsync<Registration>();
            Assert.NotNull(registration);
            var tenantId = Uuid.Parse(registration.TenantId, CultureInfo.InvariantCulture);
            var bootstrapDelivery = worker.Services.GetRequiredService<MockTenantInvitationDelivery>();
            string? administratorToken = null;
            var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            while (DateTimeOffset.UtcNow < deadline &&
                   !bootstrapDelivery.TryGetLatest(tenantId, administratorEmail, out administratorToken))
                await Task.Delay(250);
            Assert.NotNull(administratorToken);
            using var administratorClient = factory.CreateClient();
            var administratorId = await TenantInvitationE2ETests.LoginAsync(administratorClient,
                administratorEmail);
            await TenantInvitationE2ETests.VerifyEmailAsync(factory, administratorClient,
                administratorId, administratorEmail);
            using var administratorAccepted = await administratorClient.PostAsJsonAsync(
                $"/api/v1/tenants/{tenantId}/invitations/acceptance",
                new { email_address = administratorEmail, token = administratorToken });
            Assert.Equal(HttpStatusCode.NoContent, administratorAccepted.StatusCode);

            var email = $"invitee-{Guid.NewGuid():N}@example.com";
            var path = $"/api/v1/tenants/{tenantId}/invitations";
            var request = new { email_address = email, affiliation = "client_personnel" };

            using var first = await operatorClient.PostAsJsonAsync(path, request);
            Assert.Equal(HttpStatusCode.InternalServerError, first.StatusCode);
            Assert.False(delivery.TryGetLatest(tenantId, email, out _));
            using var retry = await operatorClient.PostAsJsonAsync(path, request);
            Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
            Assert.True(delivery.TryGetLatest(tenantId, email, out var token));
            Assert.False(string.IsNullOrEmpty(token));
            Assert.Equal(2, delivery.Attempts);

            using var inviteeClient = factory.CreateClient();
            var inviteeId = await TenantInvitationE2ETests.LoginAsync(inviteeClient, email);
            await TenantInvitationE2ETests.VerifyEmailAsync(factory, inviteeClient,
                inviteeId, email);
            using var accepted = await inviteeClient.PostAsJsonAsync(
                $"/api/v1/tenants/{tenantId}/invitations/acceptance",
                new { email_address = email, token });
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
            Members? members = null;
            deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await operatorClient.GetAsync(
                    $"/api/v1/tenants/{tenantId}/members");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    members = await response.Content.ReadFromJsonAsync<Members>();
                    if (members?.Items.Count == 2)
                        break;
                }
                await Task.Delay(250);
            }
            Assert.Equal(2, members?.Items.Count);
            Assert.Contains(members?.Items ?? [], member => member.UserId == administratorId);
            Assert.Contains(members?.Items ?? [], member => member.UserId == inviteeId);
        }
        finally
        {
            await worker.StopAsync();
        }
    }

    [Fact]
    public async Task ShouldRecoverInvitationGivenFirstAdministratorDeliveryFailure()
    {
        // Arrange
        var applicationName = $"compliance-split-retry-{Guid.NewGuid():N}";
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        var delivery = new FailingOnceInvitationDelivery();
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        builder.Services.AddSingleton<ITenantInvitationDelivery>(delivery);
        using var worker = builder.Build();

        // Act
        await worker.StartAsync();

        // Assert
        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient operatorClient;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
                operatorClient = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
            }
            using var operatorClientToDispose = operatorClient;
            using var administratorClient = factory.CreateClient();
            await TenantInvitationE2ETests.LoginAsync(operatorClient,
                $"operator-{Guid.NewGuid():N}@example.com");
            var administratorEmail = $"administrator-{Guid.NewGuid():N}@example.com";
            using var registered = await operatorClient.PostAsJsonAsync("/api/v1/tenants", new
            {
                name = "Delivery Retry",
                legal_name = "Delivery Retry LLC",
                slug = $"delivery-retry-{Guid.NewGuid():N}"[..24],
                first_administrator_email = administratorEmail,
            });
            Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
            var registration = await registered.Content.ReadFromJsonAsync<Registration>();
            Assert.NotNull(registration);
            var tenantId = Uuid.Parse(registration.TenantId, CultureInfo.InvariantCulture);

            string? invitationToken = null;
            var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
            while (DateTimeOffset.UtcNow < deadline &&
                   !delivery.TryGetLatest(tenantId, administratorEmail, out invitationToken))
                await Task.Delay(250);
            Assert.NotNull(invitationToken);
            Assert.True(delivery.Attempts >= 2);

            var administratorId = await TenantInvitationE2ETests.LoginAsync(administratorClient,
                administratorEmail);
            await TenantInvitationE2ETests.VerifyEmailAsync(factory, administratorClient,
                administratorId, administratorEmail);
            using var accepted = await administratorClient.PostAsJsonAsync(
                $"/api/v1/tenants/{tenantId}/invitations/acceptance",
                new { email_address = administratorEmail, token = invitationToken });
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
            using var replayedAcceptance = await administratorClient.PostAsJsonAsync(
                $"/api/v1/tenants/{tenantId}/invitations/acceptance",
                new { email_address = administratorEmail, token = invitationToken });
            Assert.Equal(HttpStatusCode.NoContent, replayedAcceptance.StatusCode);
            var administratorsTeamId = BuiltInRbac.AdministratorsTeamId(tenantId);
            var access = HttpStatusCode.Forbidden;
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await administratorClient.GetAsync(
                    $"/api/v1/tenants/{tenantId}/teams/{administratorsTeamId}");
                access = response.StatusCode;
                if (access == HttpStatusCode.OK)
                    break;
                await Task.Delay(250);
            }
            Assert.Equal(HttpStatusCode.OK, access);
            Members? members = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await operatorClient.GetAsync($"/api/v1/tenants/{tenantId}/members");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    members = await response.Content.ReadFromJsonAsync<Members>();
                    if (members?.Items.Count == 1)
                        break;
                }
                await Task.Delay(250);
            }
            Assert.Single(members?.Items ?? []);
        }
        finally
        {
            await worker.StopAsync();
        }
    }

    [Fact]
    public async Task ShouldCompleteAdministratorAndSlugFlowGivenIndependentWorker()
    {
        // Arrange
        var applicationName = $"compliance-split-invite-{Guid.NewGuid():N}";
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        using var worker = builder.Build();

        // Act
        await worker.StartAsync();

        // Assert
        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
            var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
            HttpClient operatorClient;
            try
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
                operatorClient = factory.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
            }
            using var operatorClientToDispose = operatorClient;
            using var administratorClient = factory.CreateClient();
            var operatorEmail = $"operator-{Guid.NewGuid():N}@example.com";
            var administratorEmail = $"administrator-{Guid.NewGuid():N}@example.com";
            await TenantInvitationE2ETests.LoginAsync(operatorClient, operatorEmail);

            var slug = $"split-invite-{Guid.NewGuid():N}"[..24];
            using var registered = await operatorClient.PostAsJsonAsync("/api/v1/tenants", new
            {
                name = "Split Invitation",
                legal_name = "Split Invitation LLC",
                slug,
                first_administrator_email = administratorEmail,
            });
            Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
            var registration = await registered.Content.ReadFromJsonAsync<Registration>();
            Assert.NotNull(registration);
            var tenantId = Uuid.Parse(registration.TenantId, CultureInfo.InvariantCulture);
            var administratorsTeamId = BuiltInRbac.AdministratorsTeamId(tenantId);

            await using (var restrictedFactory = E2EAppFactory.Create(broker, applicationName)
                             .WithWebHostBuilder(host => host.ConfigureTestServices(services =>
                                 services.AddSingleton(new PlatformOperatorAuthority([])))))
            {
                var priorMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
                HttpClient nonOperator;
                try
                {
                    Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
                    nonOperator = restrictedFactory.CreateClient();
                }
                finally
                {
                    Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", priorMode);
                }
                using (nonOperator)
                {
                    await TenantInvitationE2ETests.LoginAsync(nonOperator,
                        $"nonoperator-{Guid.NewGuid():N}@example.com");
                    using var createDenied = await nonOperator.PostAsJsonAsync("/api/v1/tenants", new
                    {
                        name = "Unauthorized Tenant",
                        slug = $"unauthorized-{Guid.NewGuid():N}"[..24],
                    });
                    using var inviteDenied = await nonOperator.PostAsJsonAsync(
                        $"/api/v1/tenants/{tenantId}/invitations",
                        new { email_address = "denied@example.com", affiliation = "firm_staff" });
                    using var slugDenied = await nonOperator.PostAsJsonAsync(
                        $"/api/v1/tenants/{tenantId}/slug-changes", new { slug = "denied-slug" });
                    using var suspendDenied = await nonOperator.PostAsync(
                        $"/api/v1/tenants/{tenantId}/suspensions", null);
                    using var reactivateDenied = await nonOperator.DeleteAsync(
                        $"/api/v1/tenants/{tenantId}/suspensions");
                    using var viewDenied = await nonOperator.GetAsync($"/api/v1/tenants/{tenantId}");
                    Assert.Equal(HttpStatusCode.Forbidden, createDenied.StatusCode);
                    Assert.Equal(HttpStatusCode.Forbidden, inviteDenied.StatusCode);
                    Assert.Equal(HttpStatusCode.Forbidden, slugDenied.StatusCode);
                    Assert.Equal(HttpStatusCode.Forbidden, suspendDenied.StatusCode);
                    Assert.Equal(HttpStatusCode.Forbidden, reactivateDenied.StatusCode);
                    Assert.Equal(HttpStatusCode.NotFound, viewDenied.StatusCode);
                }
            }

            var delivery = worker.Services.GetRequiredService<MockTenantInvitationDelivery>();
            var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            string? invitationToken = null;
            while (DateTimeOffset.UtcNow < deadline &&
                   !delivery.TryGetLatest(tenantId, administratorEmail, out invitationToken))
                await Task.Delay(250);
            Assert.NotNull(invitationToken);

            var administratorId = await TenantInvitationE2ETests.LoginAsync(administratorClient,
                administratorEmail);
            var acceptancePath = $"/api/v1/tenants/{tenantId}/invitations/acceptance";
            using var unverified = await administratorClient.PostAsJsonAsync(acceptancePath,
                new { email_address = administratorEmail, token = invitationToken });
            Assert.Equal(HttpStatusCode.Forbidden, unverified.StatusCode);
            await TenantInvitationE2ETests.VerifyEmailAsync(factory, administratorClient,
                administratorId, administratorEmail);
            using var accepted = await administratorClient.PostAsJsonAsync(acceptancePath,
                new { email_address = administratorEmail, token = invitationToken });
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

            var access = HttpStatusCode.Forbidden;
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await administratorClient.GetAsync(
                    $"/api/v1/tenants/{tenantId}/teams/{administratorsTeamId}");
                access = response.StatusCode;
                if (access == HttpStatusCode.OK)
                    break;
                await Task.Delay(250);
            }
            Assert.Equal(HttpStatusCode.OK, access);
            using var administratorTenant = await administratorClient.GetAsync($"/api/v1/tenants/{tenantId}");
            Assert.Equal(HttpStatusCode.OK, administratorTenant.StatusCode);

            var staffEmail = $"staff-{Guid.NewGuid():N}@example.com";
            using var invitedStaff = await operatorClient.PostAsJsonAsync(
                $"/api/v1/tenants/{tenantId}/invitations",
                new { email_address = staffEmail, affiliation = "firm_staff", administrator = false });
            Assert.Equal(HttpStatusCode.NoContent, invitedStaff.StatusCode);
            string? staffToken = null;
            var apiDelivery = factory.Services.GetRequiredService<MockTenantInvitationDelivery>();
            Assert.False(apiDelivery.TryGetLatest(tenantId, "denied@example.com", out _));
            deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            while (DateTimeOffset.UtcNow < deadline &&
                   !apiDelivery.TryGetLatest(tenantId, staffEmail, out staffToken))
                await Task.Delay(250);
            Assert.NotNull(staffToken);
            using var staffClient = factory.CreateClient();
            var staffId = await TenantInvitationE2ETests.LoginAsync(staffClient, staffEmail);
            await TenantInvitationE2ETests.VerifyEmailAsync(factory, staffClient, staffId, staffEmail);
            using var staffAccepted = await staffClient.PostAsJsonAsync(acceptancePath,
                new { email_address = staffEmail, token = staffToken });
            Assert.Equal(HttpStatusCode.NoContent, staffAccepted.StatusCode);
            Members? members = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await operatorClient.GetAsync($"/api/v1/tenants/{tenantId}/members");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    members = await response.Content.ReadFromJsonAsync<Members>();
                    if (members?.Items.Count == 2)
                        break;
                }
                await Task.Delay(250);
            }
            Assert.NotNull(members);
            Assert.Contains(members.Items, member => member.UserId == staffId &&
                member.Affiliation == "firm_staff");
            using var staffAdministratorDenied = await staffClient.GetAsync(
                $"/api/v1/tenants/{tenantId}/teams/{administratorsTeamId}");
            Assert.Equal(HttpStatusCode.Forbidden, staffAdministratorDenied.StatusCode);

            var newSlug = $"split-renamed-{Guid.NewGuid():N}"[..24];
            using var changed = await operatorClient.PostAsJsonAsync(
                $"/api/v1/tenants/{tenantId}/slug-changes", new { slug = newSlug });
            Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
            SlugResolution? oldLink = null;
            deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await administratorClient.GetAsync(
                    $"/api/v1/tenant-slugs/{slug}/mine");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    oldLink = await response.Content.ReadFromJsonAsync<SlugResolution>();
                    if (oldLink?.Redirect == true && oldLink.CurrentSlug == newSlug)
                        break;
                }
                await Task.Delay(250);
            }
            Assert.Equal(newSlug, oldLink?.CurrentSlug);
            Assert.True(oldLink?.Redirect);
            using var reused = await operatorClient.PostAsJsonAsync("/api/v1/tenants",
                new { name = "Reused Slug", slug });
            Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);

            using var suspended = await operatorClient.PostAsync(
                $"/api/v1/tenants/{tenantId}/suspensions", null);
            Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
            using var deniedImmediately = await administratorClient.GetAsync(
                $"/api/v1/tenants/{tenantId}/teams/{administratorsTeamId}");
            Assert.Equal(HttpStatusCode.Forbidden, deniedImmediately.StatusCode);
            using var reactivated = await operatorClient.DeleteAsync(
                $"/api/v1/tenants/{tenantId}/suspensions");
            Assert.Equal(HttpStatusCode.NoContent, reactivated.StatusCode);
            using var restored = await administratorClient.GetAsync(
                $"/api/v1/tenants/{tenantId}/teams/{administratorsTeamId}");
            Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        }
        finally
        {
            await worker.StopAsync();
        }
    }

    [Fact]
    public async Task ShouldProjectTenantLifecycleGivenIndependentApiAndWorker()
    {
        // Arrange
        var applicationName = $"compliance-split-e2e-{Guid.NewGuid():N}";
        var previousMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");

        // Act
        using var worker = StartWorker(applicationName);

        // Assert
        try
        {
            await using var factory = E2EAppFactory.Create(broker, applicationName);
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
            using var clientToDispose = client;
            using var login = await client.PostAsJsonAsync("/api/v1/developer-user-sessions",
                new { email_address = $"operator-{Guid.NewGuid():N}@example.com" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            var slug = $"split-{Guid.NewGuid():N}"[..24];
            using var registered = await client.PostAsJsonAsync("/api/v1/tenants", new
            {
                name = "Split Host Tenant",
                legal_name = "Split Host Tenant LLC",
                slug,
            });
            Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
            var registration = await registered.Content.ReadFromJsonAsync<Registration>();
            Assert.NotNull(registration);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            Tenant? tenant = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                Assert.False(worker.HasExited, "The independent worker exited before projection completed.");
                using var response = await client.GetAsync($"/api/v1/tenants/{registration.TenantId}");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    tenant = await response.Content.ReadFromJsonAsync<Tenant>();
                    if (tenant?.Status == "active")
                        break;
                }
                await Task.Delay(250);
            }
            Assert.Equal("Split Host Tenant LLC", tenant?.LegalName);
            Assert.Equal("active", tenant?.Status);

            using var otherClient = factory.CreateClient();
            using var otherLogin = await otherClient.PostAsJsonAsync("/api/v1/developer-user-sessions",
                new { email_address = $"other-{Guid.NewGuid():N}@example.com" });
            Assert.Equal(HttpStatusCode.OK, otherLogin.StatusCode);
            using var denied = await otherClient.GetAsync($"/api/v1/tenants/{registration.TenantId}/teams");
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            using var missing = await otherClient.GetAsync($"/api/v1/tenants/{Uuid.CreateVersion4()}/teams");
            Assert.Equal(denied.StatusCode, missing.StatusCode);
            var deniedProblem = await denied.Content.ReadFromJsonAsync<Problem>();
            var missingProblem = await missing.Content.ReadFromJsonAsync<Problem>();
            Assert.Equal(deniedProblem?.Title, missingProblem?.Title);
            Assert.Equal(deniedProblem?.Detail, missingProblem?.Detail);
            using var ownTenants = await otherClient.GetAsync("/api/v1/tenants/mine");
            Assert.Equal(HttpStatusCode.OK, ownTenants.StatusCode);
            var ownTenantList = await ownTenants.Content.ReadFromJsonAsync<TenantList>();
            Assert.Empty(ownTenantList?.Items ?? []);

            using var suspended = await client.PostAsync(
                $"/api/v1/tenants/{registration.TenantId}/suspensions", null);
            Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
            Tenant? projected = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                Assert.False(worker.HasExited, "The independent worker exited before suspension projected.");
                using var response = await client.GetAsync($"/api/v1/tenants/{registration.TenantId}");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    projected = await response.Content.ReadFromJsonAsync<Tenant>();
                    if (projected?.Status == "suspended")
                        break;
                }
                await Task.Delay(250);
            }
            Assert.Equal("suspended", projected?.Status);

            var contestedSlug = $"contested-{Guid.NewGuid():N}"[..24];
            var firstRegistration = client.PostAsJsonAsync("/api/v1/tenants",
                new { name = "First Claim", slug = contestedSlug });
            var secondRegistration = client.PostAsJsonAsync("/api/v1/tenants",
                new { name = "Second Claim", slug = contestedSlug });
            using var firstClaim = await firstRegistration;
            using var secondClaim = await secondRegistration;
            var claims = new[] { firstClaim, secondClaim };
            Assert.All(claims, claim => Assert.True(
                claim.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));
            var acceptedClaims = new List<Registration>();
            foreach (var claim in claims.Where(claim => claim.StatusCode == HttpStatusCode.OK))
            {
                var acceptedClaim = await claim.Content.ReadFromJsonAsync<Registration>();
                Assert.NotNull(acceptedClaim);
                acceptedClaims.Add(acceptedClaim);
            }
            Assert.NotEmpty(acceptedClaims);

            var resolvedStatuses = new Dictionary<string, string>();
            var claimDeadline = DateTimeOffset.UtcNow.AddSeconds(45);
            while (DateTimeOffset.UtcNow < claimDeadline && resolvedStatuses.Count < acceptedClaims.Count)
            {
                foreach (var claim in acceptedClaims)
                {
                    if (resolvedStatuses.ContainsKey(claim.TenantId))
                        continue;
                    using var response = await client.GetAsync($"/api/v1/tenants/{claim.TenantId}");
                    if (response.StatusCode != HttpStatusCode.OK)
                        continue;
                    var view = await response.Content.ReadFromJsonAsync<Tenant>();
                    if (view?.Status is "active" or "rejected")
                        resolvedStatuses.Add(claim.TenantId, view.Status);
                }
                if (resolvedStatuses.Count < acceptedClaims.Count)
                    await Task.Delay(250);
            }
            Assert.Equal(acceptedClaims.Count, resolvedStatuses.Count);
            Assert.Single(resolvedStatuses.Values, status => status == "active");
            Assert.All(resolvedStatuses.Values, status =>
                Assert.True(status is "active" or "rejected"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", previousMode);
            if (!worker.HasExited)
            {
                worker.Kill(entireProcessTree: true);
                await worker.WaitForExitAsync();
            }
        }
    }

    Process StartWorker(string applicationName)
    {
        var start = new ProcessStartInfo("dotnet");
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.Environment["COMPLIANCE_HOST_MODE"] = "worker";
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["BDGRZ_DEVELOPER_AUTH"] = "true";
        start.Environment["Fitz__Endpoint"] = broker.WebSocketEndpoint;
        start.Environment["Fitz__ApplicationName"] = applicationName;
        start.Environment["Fitz__StartupTimeoutSeconds"] = "30";
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start the worker host.");
    }

    sealed record Registration([property: JsonPropertyName("tenant_id")] string TenantId);
    sealed record Tenant([property: JsonPropertyName("legal_name")] string? LegalName, string Status);
    sealed record TenantList(IReadOnlyList<Tenant> Items);
    sealed record SlugResolution([property: JsonPropertyName("current_slug")] string CurrentSlug,
        bool Redirect);
    sealed record Members(IReadOnlyList<Member> Items);
    sealed record Member([property: JsonPropertyName("user_id")] string UserId,
        string Affiliation);
    sealed record Problem(string? Title, string? Detail);

    sealed class FailingOnceInvitationDelivery : ITenantInvitationDelivery
    {
        readonly MockTenantInvitationDelivery _successfulDeliveries = new();
        int _attempts;

        public int Attempts => Volatile.Read(ref _attempts);

        public ValueTask SendAsync(Uuid tenantId, string emailAddress, string token, CancellationToken ct)
        {
            if (Interlocked.Increment(ref _attempts) == 1)
                throw new InvalidOperationException("Injected invitation delivery failure.");
            return _successfulDeliveries.SendAsync(tenantId, emailAddress, token, ct);
        }

        public bool TryGetLatest(Uuid tenantId, string emailAddress, out string? token) =>
            _successfulDeliveries.TryGetLatest(tenantId, emailAddress, out token);
    }
}
