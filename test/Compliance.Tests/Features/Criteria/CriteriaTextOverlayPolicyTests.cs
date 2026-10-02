using Bdgrz.Compliance.Features.Criteria;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria;

public sealed class CriteriaTextOverlayPolicyTests
{
    [Theory]
    [InlineData(true, true, true, true, false, true)]
    [InlineData(true, false, true, false, false, true)]
    [InlineData(true, false, true, false, true, false)]
    [InlineData(true, true, false, false, false, false)]
    [InlineData(false, false, true, true, false, false)]
    public void ShouldEnforceBothRetainedAndCurrentLicenseGivenRequestedOperation(bool display, bool export,
        bool currentDisplay, bool currentExport, bool exportOperation, bool allowed)
    {
        // Arrange
        var original = CriteriaCatalog.Platform.GetEntry(CriteriaCatalog.Platform.Edition.EditionId, "CC1.1")!;
        var tenantId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Synthetic overlay author");
        var retained = new CriteriaTextOverlayRevision(tenantId, original.EditionId, original.Identifier, 1,
            new CriteriaTextOverlayContent("Synthetic supplied test text", "Test supplier", "Test license",
                new CriteriaOverlayUsageFlags(display, export)), actor, DateTimeOffset.UtcNow);
        var current = retained with
        {
            Revision = 2,
            Content = retained.Content with
            {
                UsageFlags = new CriteriaOverlayUsageFlags(currentDisplay, currentExport),
            }
        };

        // Act
        var result = CriteriaTextOverlayPolicy.Apply(original, retained, current, exportOperation);

        // Assert
        Assert.Equal(allowed ? retained.Content.Text : null, result.LicensedText);
        Assert.Equal(original.Summary, result.Summary);
        Assert.Equal(retained.Revision, result.Overlay!.Revision);
        Assert.Equal(display && currentDisplay, result.Overlay.AllowedUses.Display);
        Assert.Equal(export && currentExport, result.Overlay.AllowedUses.Export);
    }
}
