using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.E2E;

[Collection(BrokerCollectionDefinition.Name)]
[Trait("Category", "BrokerIntegration")]
public sealed class SplitHostInvitationDeliveryE2ETests(BrokerStackFixture broker)
    : IClassFixture<BrokerStackFixture>
{
    [Fact]
    public async Task ShouldDeliverCommittedInvitationGivenWorkerRestart()
    {
        // Arrange
        var applicationName = $"compliance-invitation-restart-{Guid.NewGuid():N}";
        using var firstWorker = CreateWorker(applicationName);
        await firstWorker.StartAsync();
        await using var factory = E2EAppFactory.Create(broker, applicationName);
        var priorMode = Environment.GetEnvironmentVariable("COMPLIANCE_HOST_MODE");
        HttpClient client;
        try
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", "api");
            client = factory.CreateClient();
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMPLIANCE_HOST_MODE", priorMode);
        }
        using var administrator = client;
        await TenantInvitationE2ETests.LoginAsync(administrator,
            $"administrator-{Guid.NewGuid():N}@example.com");
        using var registered = await administrator.PostAsJsonAsync("/api/v1/tenants", new
        {
            name = "Invitation Restart",
            slug = $"invite-restart-{Guid.NewGuid():N}"[..24],
        });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        var registration = await registered.Content.ReadFromJsonAsync<Registration>();
        Assert.NotNull(registration);
        var tenantId = Uuid.Parse(registration.TenantId, CultureInfo.InvariantCulture);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        var adminAccess = HttpStatusCode.Forbidden;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await administrator.GetAsync(
                $"/api/v1/tenants/{tenantId}/teams/{BuiltInRbac.AdministratorsTeamId(tenantId)}");
            adminAccess = response.StatusCode;
            if (adminAccess == HttpStatusCode.OK)
                break;
            await Task.Delay(250);
        }
        Assert.Equal(HttpStatusCode.OK, adminAccess);

        // Act: no worker is present when the API commits the invitation.
        await firstWorker.StopAsync();
        var invitee = $"member-{Guid.NewGuid():N}@example.com";
        using var invited = await administrator.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/member-invitations", new
            {
                email_address = invitee,
                built_in_role = BuiltInRbac.ComplianceParticipationRole,
            });
        Assert.Equal(HttpStatusCode.NoContent, invited.StatusCode);
        Assert.False(factory.Services.GetRequiredService<MockTenantInvitationDelivery>()
            .TryGetLatest(tenantId, invitee, out _));
        using var restartedWorker = CreateWorker(applicationName);
        await restartedWorker.StartAsync();

        // Assert
        try
        {
            var delivery = restartedWorker.Services.GetRequiredService<MockTenantInvitationDelivery>();
            string? token = null;
            deadline = DateTimeOffset.UtcNow.AddSeconds(45);
            while (DateTimeOffset.UtcNow < deadline &&
                   !delivery.TryGetLatest(tenantId, invitee, out token))
                await Task.Delay(250);
            Assert.False(string.IsNullOrEmpty(token));

            InvitationPage? page = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                using var response = await administrator.GetAsync(
                    $"/api/v1/tenants/{tenantId}/member-invitations?email_address=" +
                    Uri.EscapeDataString(invitee));
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    page = await response.Content.ReadFromJsonAsync<InvitationPage>();
                    if (page?.Items.SingleOrDefault()?.DeliveryStatus == "delivered")
                        break;
                }
                await Task.Delay(250);
            }
            Assert.Equal("delivered", Assert.Single(page!.Items).DeliveryStatus);
            ActorReference? deliveryActor = null;
            await foreach (var record in restartedWorker.Services.GetRequiredService<IEventStore>()
                               .ReadAsync(new TenantInvitation(tenantId, invitee).Stream,
                                   0, CancellationToken.None))
            {
                if (record.Event is TenantInvitationDeliverySent)
                    deliveryActor = ActorReference.FromSystemMetadata(record.Event.Metadata);
            }
            Assert.Equal(ActorReference.ForSystemProcess("reactor:TenantInvitationDeliveryV1",
                "TenantInvitationDeliveryV1"), deliveryActor);
        }
        finally
        {
            await restartedWorker.StopAsync();
        }
    }

    IHost CreateWorker(string applicationName)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Development",
        });
        builder.Configuration["Fitz:Endpoint"] = broker.WebSocketEndpoint;
        builder.Configuration["Fitz:ApplicationName"] = applicationName;
        builder.Configuration["Fitz:StartupTimeoutSeconds"] = "30";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        return builder.Build();
    }

    sealed record Registration([property: JsonPropertyName("tenant_id")] string TenantId);
    sealed record InvitationPage([property: JsonPropertyName("items")] Invitation[] Items);
    sealed record Invitation([property: JsonPropertyName("delivery_status")] string DeliveryStatus);
}
