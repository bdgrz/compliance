using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkDigestDispatchStatusHandlerTests
{
    [Fact]
    public async Task ShouldReadDurableDispatchStatusGivenPlatformOperator()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        var weekOf = new DateOnly(2026, 10, 5);
        var scheduledAt = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero);
        var messageId = Uuid.CreateVersion5(memberId, $"digest:{weekOf:yyyy-MM-dd}");
        await using var provider = BuildProvider(operatorId);
        await SeedDispatchAsync(provider, tenantId, memberId, weekOf, scheduledAt, messageId);
        var request = new GetWorkDigestDispatchStatus(tenantId, memberId, weekOf);

        // Act
        var result = await RequestScenario.For(provider).GivenActor(Actor(operatorId))
            .When(request).ExpectAuthorized().ExpectHandled().ExpectSuccess();

        // Assert
        Assert.IsAssignableFrom<IPlatformOperatorRequest>(request);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.Equal(memberId, result.Value.MemberId);
        Assert.Equal(weekOf, result.Value.WeekOf);
        Assert.Equal("America/New_York", result.Value.TimeZoneId);
        Assert.Equal(scheduledAt, result.Value.ScheduledAt);
        Assert.Equal("retry_pending", result.Value.Status);
        Assert.Equal(1, result.Value.Attempts);
        Assert.Equal(scheduledAt.AddMinutes(1), result.Value.LastAttemptAt);
        Assert.Equal(messageId, result.Value.MessageId);
        Assert.Equal(scheduledAt.AddMinutes(7), result.Value.NextAttemptAt);
        Assert.Equal("transient_rejection", result.Value.FailureCode);

        var serializedRequest = JsonSerializer.Serialize(request,
            ComplianceCoreJsonContext.Default.GetWorkDigestDispatchStatus);
        Assert.NotEmpty(serializedRequest);
        var serialized = JsonSerializer.Serialize(result.Value,
            ComplianceCoreJsonContext.Default.WorkDigestDispatchStatusView);
        Assert.DoesNotContain("recipient", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("body", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShouldDenyDispatchStatusGivenNonOperator()
    {
        // Arrange
        await using var provider = BuildProvider(Uuid.CreateVersion4());
        var request = new GetWorkDigestDispatchStatus(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            new DateOnly(2026, 10, 5));

        // Act
        await RequestScenario.For(provider).GivenActor(Actor(Uuid.CreateVersion4()))
            .When(request).ExpectDenied(RequestErrorKind.Forbidden).ExpectNotHandled();

        // Assert
    }

    [Fact]
    public async Task ShouldReturnNotScheduledStatusGivenUnknownWeek()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var memberId = Uuid.CreateVersion4();
        var operatorId = Uuid.CreateVersion4();
        await using var provider = BuildProvider(operatorId);
        await SeedDispatchAsync(provider, tenantId, memberId, new DateOnly(2026, 10, 5),
            new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero), Uuid.CreateVersion4());
        var request = new GetWorkDigestDispatchStatus(tenantId, memberId,
            new DateOnly(2026, 10, 12));

        // Act
        var result = await RequestScenario.For(provider).GivenActor(Actor(operatorId))
            .When(request).ExpectAuthorized().ExpectHandled().ExpectSuccess();

        // Assert
        Assert.Equal("not_scheduled", result.Value.Status);
        Assert.Equal(request.WeekOf, result.Value.WeekOf);
    }

    static ServiceProvider BuildProvider(Uuid operatorId) => ProgramManagementServices.Build(
        new DigestStatusPermissions(), portia => portia
            .AddRequestHandler<GetWorkDigestDispatchStatusHandler>()
            .AddRequestAuthorizer<PlatformOperatorAuthorizer>(),
        services => services.AddSingleton<IPlatformOperatorAccess>(
            new DigestStatusOperatorAccess(operatorId)));

    static Task SeedDispatchAsync(IServiceProvider provider, Uuid tenantId, Uuid memberId,
        DateOnly weekOf, DateTimeOffset scheduledAt, Uuid messageId) =>
        ProgramManagementServices.SeedAsync(provider, new WorkDigestDispatch(tenantId, memberId),
            dispatch => dispatch.Schedule(new WorkDigestScheduleWindow(weekOf,
                        "America/New_York", scheduledAt), messageId,
                    retryWindow: TimeSpan.FromHours(24), recordedAt: scheduledAt.AddMinutes(-1)) &&
                    dispatch.StartAttempt(weekOf, scheduledAt.AddMinutes(1)) &&
                    dispatch.RecordTransientRejection(weekOf, scheduledAt.AddMinutes(2),
                        maximumAttempts: 3, retryDelay: TimeSpan.FromMinutes(5))
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The test digest dispatch could not be seeded.")));

    static ClaimsPrincipal Actor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed class DigestStatusPermissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(true);
    }

    sealed class DigestStatusOperatorAccess(Uuid operatorId) : IPlatformOperatorAccess
    {
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(operatorId == userId);
    }
}
