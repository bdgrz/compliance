using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Versioning;

public sealed class VersionedRecordRulesTests
{
    [Fact]
    public void ShouldReturnComplianceConflictFactsGivenStaleVersionsAndReferencedDraft()
    {
        // Arrange
        var draftVersionId = Uuid.CreateVersion4();
        var approvedVersionId = Uuid.CreateVersion4();

        // Act
        var revision = VersionedRecordRules.StaleRevision("program", 7);
        var draft = VersionedRecordRules.StaleDraft("boundary", draftVersionId, 3);
        var approved = VersionedRecordRules.StaleApprovedVersion("boundary", approvedVersionId);
        var referenced = VersionedRecordRules.DraftDiscardConflict(true);
        var unreferenced = VersionedRecordRules.DraftDiscardConflict(false);

        // Assert
        Assert.Equal(new VersionConflict(VersionConflictCode.StaleRevision, "program", 7),
            revision);
        Assert.Equal(new VersionConflict(VersionConflictCode.StaleDraft, "boundary", 3,
            draftVersionId), draft);
        Assert.Equal(new VersionConflict(VersionConflictCode.StaleApprovedVersion, "boundary",
            CurrentVersionId: approvedVersionId), approved);
        Assert.Equal(new VersionConflict(VersionConflictCode.ReferencedDraft, "draft"),
            referenced);
        Assert.Null(unreferenced);
    }
}
