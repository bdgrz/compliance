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
}
