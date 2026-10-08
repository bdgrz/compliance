using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class FirmStaffRetainedStatusTests
{
    [Fact]
    public async Task ShouldVerifyOriginalStatusGivenLaterDirectoryChanges()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build(), developerAuthentication: true);
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var staffId = Uuid.CreateVersion4();
        var actor = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Operator");
        var recordedAt = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        var requestId = Uuid.CreateVersion4();
        FirmStaffMemberView? original = null;
        await ProgramManagementServices.SeedAsync(provider, new FirmStaffDirectory(), directory =>
        {
            Assert.True(directory.Register(Uuid.CreateVersion4(), staffId, Uuid.CreateVersion4(), "attest",
                "Explicit test directory source", 0, actor, recordedAt).IsSuccess);
            var status = directory.SetStatus(requestId, staffId, false, "Original status source", 1,
                actor, recordedAt.AddMinutes(1));
            Assert.True(status.IsSuccess);
            original = status.Value;
            return directory.SetStatus(Uuid.CreateVersion4(), staffId, true, "Later status source", 2,
                actor, recordedAt.AddMinutes(2));
        });
        var cause = new FirmStaffChangeRecorded(requestId, 1, "status", "Original status source", original!);

        // Act
        var retained = await ProgramManagementServices.HydrateAsync(provider, new FirmStaffDirectory());

        // Assert
        Assert.True(retained.Get(staffId)!.IsActive);
        Assert.True(retained.MatchesRetainedStatus(cause));
        Assert.False(retained.MatchesRetainedStatus(cause with { RequestId = Uuid.CreateVersion4() }));
        Assert.False(retained.MatchesRetainedStatus(cause with { ExpectedSequence = 2 }));
        Assert.False(retained.MatchesRetainedStatus(cause with { Reason = "Another status reason" }));
        Assert.False(retained.MatchesRetainedStatus(cause with { Operation = "register" }));
        Assert.False(retained.MatchesRetainedStatus(cause with { Staff = cause.Staff with { IsActive = true } }));
        Assert.False(retained.MatchesRetainedStatus(cause with { Staff = cause.Staff with { UserId = Uuid.CreateVersion4() } }));
        Assert.False(retained.MatchesRetainedStatus(cause with { Staff = cause.Staff with { Actor = actor with { Id = Uuid.CreateVersion4().ToString() } } }));
        Assert.False(retained.MatchesRetainedStatus(cause with { Staff = cause.Staff with { RecordedAt = recordedAt } }));
    }
}
