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

    [Fact]
    public void ShouldRequestSlugRegistrationGivenTenantRegistration()
    {
        var tenant = new Tenant(TenantId);
        var scenario = new AggregateScenario<Tenant>(tenant);

        var result = tenant.Register(OwnerUserId, "Acme, Inc.", "acme");

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
    public void ShouldConfirmOrRejectTheRequestedSlugFromChoreography()
    {
        var confirmed = new Tenant(TenantId);
        _ = confirmed.Register(OwnerUserId, "Acme", "acme");
        var confirmedScenario = new AggregateScenario<Tenant>(confirmed);

        var confirmResult = confirmed.ConfirmSlug("acme");

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
    public void ShouldRequestSurrenderOnlyForConfirmedSlug()
    {
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");

        var beforeConfirmation = tenant.RequestSlugSurrender("acme");
        _ = tenant.ConfirmSlug("acme");
        var afterConfirmation = tenant.RequestSlugSurrender("acme");

        Assert.False(beforeConfirmation.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, beforeConfirmation.Error.Kind);
        Assert.True(afterConfirmation.IsSuccess);
        Assert.IsType<TenantSlugSurrenderRequested>(
            new AggregateScenario<Tenant>(tenant).PendingEvents[^1]);
    }

    [Fact]
    public void ShouldRestoreConfirmedSlugGivenSurrenderIsRejected()
    {
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        _ = tenant.ConfirmSlug("acme");
        _ = tenant.RequestSlugSurrender("acme");

        var rejected = tenant.RejectSlugSurrender("acme");
        var retry = tenant.RequestSlugSurrender("acme");

        Assert.True(rejected.IsSuccess);
        Assert.True(retry.IsSuccess);
    }

    [Fact]
    public void ShouldBlockAndRestoreAccessWithoutDiscardingTenantHistory()
    {
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        Assert.False(tenant.IsActive);
        _ = tenant.ConfirmSlug("acme");
        Assert.True(tenant.IsActive);

        Assert.True(tenant.Suspend(OwnerUserId).IsSuccess);
        Assert.True(tenant.IsSuspended);
        Assert.False(tenant.IsActive);
        Assert.True(tenant.Suspend(OwnerUserId).IsSuccess);
        Assert.Single(new AggregateScenario<Tenant>(tenant).PendingEvents.OfType<TenantSuspended>());

        Assert.True(tenant.Reactivate(OwnerUserId).IsSuccess);
        Assert.True(tenant.IsActive);
        Assert.Single(new AggregateScenario<Tenant>(tenant).PendingEvents.OfType<TenantReactivated>());
    }

    [Fact]
    public void InvitedFirstAdministratorMustAcceptBeforeTenantIsActive()
    {
        var tenant = new Tenant(TenantId);
        var result = tenant.Register(OwnerUserId, "Acme", "acme", "Acme Legal LLC", "admin@example.com");
        Assert.True(result.IsSuccess);
        _ = tenant.ConfirmSlug("acme");

        Assert.False(tenant.IsActive);
        Assert.False(tenant.Suspend(OwnerUserId).IsSuccess);
        Assert.False(tenant.Activate(Uuid.CreateVersion4(), "other@example.com").IsSuccess);
        Assert.True(tenant.Activate(Uuid.CreateVersion4(), "admin@example.com").IsSuccess);
        Assert.True(tenant.IsActive);
    }

    [Fact]
    public void SlugChangeKeepsOldSlugUntilReplacementIsConfirmed()
    {
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        _ = tenant.ConfirmSlug("acme");

        Assert.True(tenant.RequestSlugChange("acme-new").IsSuccess);
        Assert.Equal("acme", tenant.CurrentSlug);
        Assert.True(tenant.ConfirmSlug("acme-new").IsSuccess);
        Assert.Equal("acme-new", tenant.CurrentSlug);
        Assert.True(tenant.ConfirmSlugSurrender("acme").IsSuccess);
        Assert.True(tenant.IsActive);
        Assert.Single(new AggregateScenario<Tenant>(tenant).PendingEvents.OfType<TenantSlugChanged>());
    }

    [Fact]
    public void RejectedSlugChangeRetainsCurrentSlug()
    {
        var tenant = new Tenant(TenantId);
        _ = tenant.Register(OwnerUserId, "Acme", "acme");
        _ = tenant.ConfirmSlug("acme");
        _ = tenant.RequestSlugChange("taken-slug");

        Assert.True(tenant.RejectSlug("taken-slug").IsSuccess);
        Assert.Equal("acme", tenant.CurrentSlug);
        Assert.True(tenant.IsActive);
    }
}
