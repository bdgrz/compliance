using System.Globalization;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantTests
{
    static readonly Uuid TenantId = Uuid.Parse(
        "46ca06ed-bddb-4283-9482-b8667ed89e86",
        CultureInfo.InvariantCulture);
    static readonly Uuid OwnerUserId = Uuid.Parse(
        "0862062f-97e9-45de-a312-0f884c48180d",
        CultureInfo.InvariantCulture);
    static readonly DateTimeOffset RecordedAt = new(2026, 10, 3, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRequestSlugRegistrationGivenTenantRegistration()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        var scenario = new AggregateScenario<Tenant>(tenant);

        // Act
        var result = tenant.Register(OwnerUserId, "Acme, Inc.", "acme");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TenantId, result.Value.TenantId);
        Assert.Equal("acme", result.Value.Slug);
        var registeredEvent = Assert.Single(scenario.PendingEvents);
        Assert.Equal("TenantRegistered", registeredEvent.GetType().Name);
        Assert.Equal(OwnerUserId, registeredEvent.GetType().GetProperty("OwnerUserId")?.GetValue(registeredEvent));
        Assert.Equal("Acme, Inc.", registeredEvent.GetType().GetProperty("Name")?.GetValue(registeredEvent));
        Assert.Equal("acme", registeredEvent.GetType().GetProperty("Slug")?.GetValue(registeredEvent));
    }

    [Fact]
    public void ShouldResolveSlugGivenChoreographyDecision()
    {
        // Arrange
        var confirmed = new Tenant(TenantId);
        _ = confirmed.Register(OwnerUserId, "Acme", "acme");
        var confirmedScenario = new AggregateScenario<Tenant>(confirmed);

        // Act
        var confirmResult = confirmed.ConfirmSlug("acme");

        // Assert
        Assert.True(confirmResult.IsSuccess);
        Assert.IsType<TenantSlugConfirmed>(confirmedScenario.PendingEvents[^1]);

        var rejected = new Tenant(TenantId);
        _ = rejected.Register(OwnerUserId, "Acme", "acme");
        var rejectedScenario = new AggregateScenario<Tenant>(rejected);

        var rejectResult = rejected.RejectSlug("acme");

        Assert.True(rejectResult.IsSuccess);
        Assert.IsType<TenantSlugRejected>(rejectedScenario.PendingEvents[^1]);
    }

    [Fact]
    public void ShouldRequestSurrenderGivenConfirmedSlug()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");

        var beforeConfirmation = tenant.RequestSlugSurrender("acme");
        _ = tenant.ConfirmSlug("acme");

        // Act
        var afterConfirmation = tenant.RequestSlugSurrender("acme");

        // Assert
        Assert.False(beforeConfirmation.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, beforeConfirmation.Error.Kind);
        Assert.True(afterConfirmation.IsSuccess);
        Assert.IsType<TenantSlugSurrenderRequested>(
            new AggregateScenario<Tenant>(tenant).PendingEvents[^1]);
    }

    [Fact]
    public void ShouldRestoreConfirmedSlugGivenSurrenderIsRejected()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        _ = tenant.ConfirmSlug("acme");
        _ = tenant.RequestSlugSurrender("acme");

        var rejected = tenant.RejectSlugSurrender("acme");

        // Act
        var retry = tenant.RequestSlugSurrender("acme");

        // Assert
        Assert.True(rejected.IsSuccess);
        Assert.True(retry.IsSuccess);
    }

    [Fact]
    public void ShouldPreserveHistoryGivenSuspensionAndReactivation()
    {
        // Arrange
        var tenant = new Tenant(TenantId);

        // Act
        _ = tenant.Register(OwnerUserId, "Acme", "acme");

        // Assert
        Assert.False(tenant.IsActive);
        _ = tenant.ConfirmSlug("acme");
        Assert.True(tenant.IsActive);

        Assert.True(tenant.Suspend(OwnerUserId, "Security review", RecordedAt).IsSuccess);
        Assert.True(tenant.IsSuspended);
        Assert.False(tenant.IsActive);
        Assert.True(tenant.Suspend(OwnerUserId, "Repeated request", RecordedAt.AddMinutes(1)).IsSuccess);
        var suspended = Assert.Single(new AggregateScenario<Tenant>(tenant).PendingEvents.OfType<TenantSuspended>());
        Assert.Equal("Security review", suspended.Reason);
        Assert.Equal(RecordedAt, suspended.OccurredAt);

        Assert.True(tenant.Reactivate(OwnerUserId, "Review complete", RecordedAt.AddHours(1)).IsSuccess);
        Assert.True(tenant.IsActive);
        var reactivated = Assert.Single(new AggregateScenario<Tenant>(tenant).PendingEvents.OfType<TenantReactivated>());
        Assert.Equal("Review complete", reactivated.Reason);
        Assert.Equal(RecordedAt.AddHours(1), reactivated.OccurredAt);
    }

    [Fact]
    public void ShouldRequireReasonGivenTenantLifecycleDecision()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        _ = tenant.ConfirmSlug("acme");
        var scenario = new AggregateScenario<Tenant>(tenant);

        // Act
        var suspend = tenant.Suspend(OwnerUserId, " ", RecordedAt);
        var offboard = tenant.StartOffboarding(OwnerUserId, new string('x', 501), RecordedAt);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, suspend.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, offboard.Error?.Kind);
        Assert.Empty(scenario.PendingEvents.OfType<TenantSuspended>());
        Assert.Empty(scenario.PendingEvents.OfType<TenantOffboardingStarted>());
    }

    [Fact]
    public void ShouldRequirePlatformOperatorGivenOffboardingRequest()
    {
        // Arrange
        var request = new OffboardTenant(TenantId, "Client requested offboarding.");

        // Act
        var platformRequest = Assert.IsAssignableFrom<IPlatformOperatorRequest>(request);

        // Assert
        Assert.Equal(TenantId, request.TenantId);
        Assert.Equal("Client requested offboarding.", request.Reason);
        Assert.NotNull(platformRequest);
    }

    [Fact]
    public void ShouldRevokeTenantAccessGivenOffboarding()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        _ = tenant.ConfirmSlug("acme");
        var scenario = new AggregateScenario<Tenant>(tenant);
        var startedAt = RecordedAt;

        // Act
        var result = tenant.StartOffboarding(OwnerUserId, "Client requested offboarding", startedAt);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(tenant.IsOffboarding);
        Assert.False(tenant.IsActive);
        var offboarding = Assert.Single(scenario.PendingEvents.OfType<TenantOffboardingStarted>());
        Assert.Equal(OwnerUserId, offboarding.OperatorUserId);
        Assert.Equal("Client requested offboarding", offboarding.Reason);
        Assert.Equal(startedAt, offboarding.StartedAt);
        Assert.True(tenant.StartOffboarding(OwnerUserId, "Repeated request", startedAt.AddMinutes(1)).IsSuccess);
        Assert.Single(scenario.PendingEvents.OfType<TenantOffboardingStarted>());
        var reactivate = tenant.Reactivate(OwnerUserId, "Cancel offboarding", startedAt.AddMinutes(1));
        Assert.Equal(RequestErrorKind.Conflict, reactivate.Error?.Kind);
    }

    [Fact]
    public void ShouldRequireAcceptanceGivenInvitedFirstAdministrator()
    {
        // Arrange
        var tenant = new Tenant(TenantId);

        // Act
        var result = tenant.Register(OwnerUserId, "Acme", "acme", "Acme Legal LLC", "admin@example.com");

        // Assert
        Assert.True(result.IsSuccess);
        _ = tenant.ConfirmSlug("acme");

        Assert.False(tenant.IsActive);
        Assert.False(tenant.Suspend(OwnerUserId, "Provisioning not complete", RecordedAt).IsSuccess);
        Assert.False(tenant.Activate(Uuid.CreateVersion4(), "other@example.com").IsSuccess);
        Assert.True(tenant.Activate(Uuid.CreateVersion4(), "admin@example.com").IsSuccess);
        Assert.True(tenant.IsActive);
    }

    [Fact]
    public void ShouldRemainProvisioningGivenConfirmedSlugForVerifiedCreator()
    {
        // Arrange
        var tenant = new Tenant(TenantId);

        // Act
        var result = tenant.Register(OwnerUserId, "Acme", "acme", "Acme LLC",
            creatorIsAdministrator: true);

        // Assert
        Assert.True(result.IsSuccess);
        var registered = Assert.Single(new AggregateScenario<Tenant>(tenant).PendingEvents
            .OfType<TenantRegistered>());
        Assert.True(registered.CreatorIsAdministrator);
        Assert.True(registered.ActivationRequired);
        Assert.Equal(OwnerUserId, registered.OwnerUserId);
        Assert.Equal(Uuid.Empty, tenant.OperatorUserId);
        var pending = tenant.Activate(OwnerUserId, null);
        Assert.False(pending.IsSuccess);
        Assert.True(pending.Error.IsTransient);
        Assert.True(tenant.ConfirmSlug("acme").IsSuccess);
        Assert.False(tenant.IsActive);
        Assert.False(tenant.Activate(OwnerUserId, "creator@example.com").IsSuccess);
        Assert.True(tenant.Activate(OwnerUserId, null).IsSuccess);
        Assert.True(tenant.IsActive);
        Assert.False(tenant.Activate(Uuid.CreateVersion4(), null).IsSuccess);
    }

    [Fact]
    public void ShouldRejectInvitationGivenCreatorAdministration()
    {
        // Arrange
        var tenant = new Tenant(TenantId);

        // Act
        var result = tenant.Register(OwnerUserId, "Acme", "acme", "Acme LLC",
            "admin@example.com", creatorIsAdministrator: true);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<Tenant>(tenant).PendingEvents);
    }

    [Fact]
    public void ShouldKeepOldSlugGivenPendingReplacement()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");

        // Act
        _ = tenant.ConfirmSlug("acme");

        // Assert
        Assert.True(tenant.RequestSlugChange("acme-new").IsSuccess);
        Assert.Equal("acme", tenant.CurrentSlug);
        Assert.True(tenant.ConfirmSlug("acme-new").IsSuccess);
        Assert.Equal("acme-new", tenant.CurrentSlug);
        Assert.True(tenant.ConfirmSlugSurrender("acme").IsSuccess);
        Assert.True(tenant.IsActive);
        Assert.Single(new AggregateScenario<Tenant>(tenant).PendingEvents.OfType<TenantSlugChanged>());
    }

    [Fact]
    public void ShouldRetainCurrentSlugGivenRejectedChange()
    {
        // Arrange
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        _ = tenant.ConfirmSlug("acme");

        // Act
        _ = tenant.RequestSlugChange("taken-slug");

        // Assert
        Assert.True(tenant.RejectSlug("taken-slug").IsSuccess);
        Assert.Equal("acme", tenant.CurrentSlug);
        Assert.True(tenant.IsActive);
    }
}
