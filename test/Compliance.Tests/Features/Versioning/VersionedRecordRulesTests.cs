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
        var draft = VersionedRecordRules.StaleDraft("boundary", draftVersionId.ToGuid(), 3);
        var approved = VersionedRecordRules.StaleApprovedVersion("boundary",
            approvedVersionId.ToGuid());
        var referenced = VersionedRecordRules.DraftDiscardConflict(true);
        var unreferenced = VersionedRecordRules.DraftDiscardConflict(false);

        // Assert
        Assert.Equal(new VersionConflict(VersionConflictCode.StaleRevision, "program", 7),
            revision);
        Assert.Equal(new VersionConflict(VersionConflictCode.StaleDraft, "boundary", 3,
            draftVersionId.ToGuid()), draft);
        Assert.Equal(new VersionConflict(VersionConflictCode.StaleApprovedVersion, "boundary",
            CurrentVersionId: approvedVersionId.ToGuid()), approved);
        Assert.IsType<Guid>(draft.CurrentVersionId);
        Assert.IsType<Guid>(approved.CurrentVersionId);
        Assert.Equal(RequestErrorKind.Conflict, draft.ToRequestError().Kind);
        Assert.Contains(draftVersionId.ToString(), draft.ToRequestError().Message,
            StringComparison.Ordinal);
        Assert.Contains("revision: 3", draft.ToRequestError().Message,
            StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.Conflict, approved.ToRequestError().Kind);
        Assert.Contains(approvedVersionId.ToString(), approved.ToRequestError().Message,
            StringComparison.Ordinal);
        Assert.Equal(new VersionConflict(VersionConflictCode.ReferencedDraft, "draft"),
            referenced);
        Assert.Null(unreferenced);
    }
}
