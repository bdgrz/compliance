using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory;

public sealed class TechnologyInventoryAggregateTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid OwnerId = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldReplayIdenticalRecordAndRejectChangedContentGivenRecordedComponent()
    {
        // Arrange
        var component = new TechnologyComponent(TenantId, Uuid.CreateVersion4());
        Assert.True(component.Record(Component("network", " Core VPC "), Author, Now).IsSuccess);

        // Act
        var replay = component.Record(Component("network", "Core VPC"), Author, Now);
        var changed = component.Record(Component("network", "Edge VPC"), Author, Now);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        var recorded = Assert.IsType<TechnologyComponentRevisionRecorded>(Assert.Single(
            new AggregateScenario<TechnologyComponent>(component).PendingEvents));
        Assert.Equal("Core VPC", recorded.Content.Name);
        Assert.Equal(TechnologyInventoryRules.Active, recorded.Content.Lifecycle);
        Assert.Equal(1, recorded.Revision);
    }

    [Theory]
    [InlineData("server", null, null, null)]
    [InlineData("cloud_account", null, null, null)]
    [InlineData("network", "instance", null, null)]
    [InlineData("endpoint_class", null, null, "MDM")]
    [InlineData("network", null, 3, null)]
    public void ShouldRejectBeforeAppendGivenInvalidComponentShape(string category,
        string? instance, int? count, string? source)
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var component = new TechnologyComponent(TenantId, id);
        var content = Component(category, "Thing") with
        {
            SystemInstanceId = instance is null ? null : id,
            EndpointCount = count,
            ManagementSource = source,
        };

        // Act
        var result = component.Record(content, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<TechnologyComponent>(component).PendingEvents);
    }

    [Fact]
    public void ShouldKeyCloudAccountBySystemInstanceGivenCloudAccountComponent()
    {
        // Arrange
        var instanceId = Uuid.CreateVersion4();
        var matching = new TechnologyComponent(TenantId, instanceId);
        var other = new TechnologyComponent(TenantId, Uuid.CreateVersion4());
        var content = Component("cloud_account", "AWS prod") with { SystemInstanceId = instanceId };

        // Act
        var accepted = matching.Record(content, Author, Now);
        var rejected = other.Record(content, Author, Now);

        // Assert
        Assert.True(accepted.IsSuccess);
        Assert.Equal(instanceId, accepted.Value.ComponentId);
        Assert.Equal(RequestErrorKind.Validation, rejected.Error!.Kind);
    }

    [Fact]
    public void ShouldRetireAndRejectStaleOrCategoryChangeGivenComponentRevision()
    {
        // Arrange
        var component = new TechnologyComponent(TenantId, Uuid.CreateVersion4());
        Assert.True(component.Record(Component("repository", "api"), Author, Now).IsSuccess);

        // Act
        var retired = component.Revise(1, current => current with
        {
            Lifecycle = TechnologyInventoryRules.Retired,
        }, Author, Now);
        var stale = component.Revise(1, current => current, Author, Now);
        var recategorized = component.Revise(2, current => current with
        {
            Category = "network",
        }, Author, Now);

        // Assert
        Assert.Null(retired);
        Assert.Equal(2, component.Revision);
        Assert.Equal(TechnologyInventoryRules.Retired, component.Content!.Lifecycle);
        Assert.Equal(CommandFailureCode.VersionConflict, stale!.Code);
        Assert.Equal(CommandFailureCode.InvalidContent, recategorized!.Code);
    }

    [Theory]
    [InlineData("secret", "M0-D16")]
    [InlineData("internal", " ")]
    public void ShouldRejectGivenInvalidClassificationOrRetention(string classification,
        string retention)
    {
        // Arrange
        var asset = new InformationAsset(TenantId, Uuid.CreateVersion4());

        // Act
        var result = asset.Record(new InformationAssetContent("Customer PII", classification,
            retention, OwnerId, null, "active"), Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
    }

    [Theory]
    [InlineData("confidential", false, true, null, false)]
    [InlineData("restricted", true, false, null, false)]
    [InlineData("restricted", false, false, "EXC-7", true)]
    [InlineData("confidential", true, true, null, true)]
    [InlineData("internal", false, false, null, true)]
    public void ShouldRequireEncryptionOrExceptionGivenSensitiveFlow(string classification,
        bool inTransit, bool atRest, string? exception, bool accepted)
    {
        // Arrange
        var flow = new DataFlow(TenantId, Uuid.CreateVersion4());

        // Act
        var result = flow.Record(Flow(classification) with
        {
            EncryptedInTransit = inTransit,
            EncryptedAtRest = atRest,
            ExceptionReference = exception,
        }, Author, Now);

        // Assert
        Assert.Equal(accepted, result.IsSuccess);
    }

    [Fact]
    public void ShouldVersionAndRejectEarlierEffectiveDateGivenFlowRevision()
    {
        // Arrange
        var flow = new DataFlow(TenantId, Uuid.CreateVersion4());
        var original = Flow("internal");
        Assert.True(flow.Record(original, Author, Now).IsSuccess);

        // Act
        var replay = flow.Record(original with { InformationAssetIds = [.. original.InformationAssetIds] },
            Author, Now);
        var earlier = flow.Revise(1, current => current with
        {
            EffectiveFrom = current.EffectiveFrom.AddDays(-1),
        }, Author, Now);
        var later = flow.Revise(1, current => current with
        {
            EffectiveFrom = current.EffectiveFrom.AddDays(30),
            Purpose = "Nightly export",
        }, Author, Now);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(CommandFailureCode.InvalidContent, earlier!.Code);
        Assert.Null(later);
        Assert.Equal(2, flow.Revision);
    }

    [Fact]
    public void ShouldNotHydrateAsAnotherKindGivenForeignInventoryStream()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var recorded = new TechnologyComponentRevisionRecorded(TenantId, id, 1,
            Component("network", "VPC"), Author, Now);

        // Act
        var asset = new AggregateScenario<InformationAsset>(new InformationAsset(TenantId, id))
            .Given(DomainEventSeed.Attach(recorded, id, 1)).Aggregate;
        var record = asset.Record(new InformationAssetContent("PII", "restricted", "M0-D16",
            OwnerId, null, "active"), Author, Now);

        // Assert
        Assert.False(asset.IsCreated);
        Assert.Equal(RequestErrorKind.Conflict, record.Error!.Kind);
    }

    static TechnologyComponentContent Component(string category, string name) =>
        new(category, name, OwnerId, "production", "us-east-1", null, null, null, "active");

    static DataFlowContent Flow(string classification) =>
        new("technology_component", Uuid.CreateVersion4(), "external_party", null, "Payroll Co",
            [Uuid.CreateVersion4()], "Payroll export", false, false, null,
            new DateOnly(2026, 10, 1), OwnerId, "active", classification);
}
