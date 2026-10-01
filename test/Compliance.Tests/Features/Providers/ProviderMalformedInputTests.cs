using System.Text.Json;
using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderMalformedInputTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("{\"name\":\"Provider\",\"provider_kind\":\"Supplier\",\"dependencies\":[null]}")]
    [InlineData("{\"name\":\"Provider\",\"provider_kind\":\"Supplier\",\"materiality_basis\":[null]}")]
    public async Task ShouldRejectMalformedNestedInputGivenRecordRevisionAndReferenceResolution(string json)
    {
        // Arrange
        var content = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.ProviderContent)!;
        var tenantId = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Recorder");
        var register = new ProviderRegister(tenantId);
        Assert.True(register.Record(id, requestId, new ProviderContent("Provider", "Supplier"), actor, DateTimeOffset.UtcNow).IsSuccess);
        var services = new ServiceCollection();
        var store = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(store);
        services.AddSingleton<IDomainEventReader>(store);
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var references = new ProviderReferences(scope.ServiceProvider.GetRequiredService<IAggregateReader>(), store);

        // Act
        var creation = register.Record(Uuid.CreateVersion4(), Uuid.CreateVersion4(), content, actor, DateTimeOffset.UtcNow);
        var revision = register.Revise(id, Uuid.CreateVersion4(), 1, content, actor, DateTimeOffset.UtcNow);
        var retry = register.CheckRetry(id, requestId, null, content);
        var resolved = await references.ResolveAsync(tenantId, content, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, creation.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, revision.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, retry!.Value.Error!.Kind);
        Assert.Equal(RequestErrorKind.Validation, resolved.Error!.Kind);
        Assert.Single(new AggregateScenario<ProviderRegister>(register).PendingEvents);
    }
}
