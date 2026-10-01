using Bdgrz.Compliance.Features.TechnologyInventory;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.TechnologyInventory;

public sealed class InformationAssetImpactTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly Uuid AssetId = Uuid.CreateVersion4();
    static readonly Uuid OtherAssetId = Uuid.CreateVersion4();

    [Fact]
    public void ShouldFlagEncryptionViolationGivenReclassificationToConfidential()
    {
        // Arrange
        var flow = Flow("internal", encrypted: false, exception: null, AssetId);

        // Act
        var impact = InformationAssetImpact.Evaluate(flow, AssetId, "confidential", "active",
            new Dictionary<Uuid, InformationAssetContent>());

        // Assert
        Assert.Equal("confidential", impact.RecomputedClassification);
        Assert.True(impact.ClassificationChanged);
        Assert.True(impact.EncryptionViolation);
        Assert.False(impact.CarriesRetiredAssetOnly);
    }

    [Fact]
    public void ShouldAcceptExceptionReferenceGivenReclassificationToRestricted()
    {
        // Arrange
        var flow = Flow("internal", encrypted: false, exception: "EXC-7", AssetId);

        // Act
        var impact = InformationAssetImpact.Evaluate(flow, AssetId, "restricted", "active",
            new Dictionary<Uuid, InformationAssetContent>());

        // Assert
        Assert.Equal("restricted", impact.RecomputedClassification);
        Assert.False(impact.EncryptionViolation);
    }

    [Fact]
    public void ShouldKeepHighestCarriedClassificationGivenDowngradeOfOneAsset()
    {
        // Arrange
        var flow = Flow("restricted", encrypted: true, exception: null, AssetId, OtherAssetId);
        var carried = new Dictionary<Uuid, InformationAssetContent>
        {
            [OtherAssetId] = Asset("confidential", "active"),
        };

        // Act
        var impact = InformationAssetImpact.Evaluate(flow, AssetId, "public", "active", carried);

        // Assert
        Assert.Equal("confidential", impact.RecomputedClassification);
        Assert.True(impact.ClassificationChanged);
        Assert.False(impact.EncryptionViolation);
    }

    [Fact]
    public void ShouldReportRetiredOnlyCarriageGivenRetirementOfSoleActiveAsset()
    {
        // Arrange
        var flow = Flow("internal", encrypted: true, exception: null, AssetId, OtherAssetId);
        var carried = new Dictionary<Uuid, InformationAssetContent>
        {
            [OtherAssetId] = Asset("internal", "retired"),
        };

        // Act
        var retired = InformationAssetImpact.Evaluate(flow, AssetId, "internal", "retired",
            carried);
        var kept = InformationAssetImpact.Evaluate(flow, AssetId, "internal", "active", carried);

        // Assert
        Assert.True(retired.CarriesRetiredAssetOnly);
        Assert.False(kept.CarriesRetiredAssetOnly);
        Assert.False(retired.ClassificationChanged);
    }

    static InformationAssetContent Asset(string classification, string lifecycle) =>
        new("Asset", classification, "M0-D16", Uuid.CreateVersion4(), null, lifecycle);

    static DataFlowView Flow(string classification, bool encrypted, string? exception,
        params Uuid[] assets) =>
        new(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1,
            new DataFlowContent("system_instance", Uuid.CreateVersion4(), "external_party", null,
                "Payroll vendor", assets, "Payroll export", encrypted, encrypted, exception,
                DateOnly.FromDateTime(Now.UtcDateTime), Uuid.CreateVersion4(), "active",
                classification), "manual", ActorReference.ForMember(Uuid.CreateVersion4(), "A"),
            Now);
}
