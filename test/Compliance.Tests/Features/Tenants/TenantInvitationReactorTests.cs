using Bdgrz.Compliance;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantInvitationReactorTests
{
    [Fact]
    public async Task ShouldBoundTenantInvitationRetryDelayGivenProjectionLag()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["Fitz:Endpoint"] = "ws://127.0.0.1:4090/ws";
        builder.Services.AddCompliance(builder.Configuration, developerAuthentication: true).AddWorkers();
        await using var provider = builder.Services.BuildServiceProvider();

        // Act
        var registration = Assert.Single(provider.GetServices<WorkloadRegistration>(),
            workload => workload.Name == "TenantInvitation");

        // Assert
        Assert.Equal(120, registration.FailureAttemptLimit);
        Assert.Equal(TimeSpan.FromSeconds(2), registration.MaximumFailureDelay);
    }

    [Fact]
    public async Task ShouldReplayAdministratorActivationGivenTransientProjectionLag()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var scenario = new ReactorScenario()
            .Given(new TenantInvitationAccepted(tenantId, userId, "admin@example.com",
                "client_personnel", Administrator: true))
            .RespondTo<ActivateTenant>(Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Membership and permission projections are not ready.")));
        var reactor = new TenantInvitationReactor(new InMemoryProjectionCheckpointStore(), scenario.Requests);

        // Act
        await Assert.ThrowsAsync<ReactionCommandFailedException>(async () => await scenario.RunAsync(reactor));
        scenario.RespondTo<ActivateTenant>(Result.Success);
        await scenario.RunAsync(reactor);

        // Assert
        Assert.Equal(2, scenario.SentRequests.Count(request => request is RegisterMember));
        Assert.Equal(2, scenario.SentRequests.Count(request => request is AssignTeamMember));
        Assert.Equal(2, scenario.SentRequests.Count(request => request is ActivateTenant));
    }
}
