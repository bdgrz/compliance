using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution;

public sealed class TrainingCatalogTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    static TrainingRequirementContent Content(string course = "Security awareness 2026",
        string source = "lms_export") =>
        new(course, null, PolicyAudience.CoreSecurity, null, source);

    [Fact]
    public void ShouldKeepImmutableVersionsGivenRevisedRequirement()
    {
        // Arrange
        var catalog = new TrainingCatalog(TenantId, ProgramId);
        var request = Uuid.CreateVersion4();
        var defined = catalog.Define(request, " sat ", Content(), Actor, Now);

        // Act
        var retry = catalog.Define(request, "SAT", Content(), Actor, Now);
        var duplicate = catalog.Define(Uuid.CreateVersion4(), "SAT", Content("Other"), Actor, Now);
        var stale = catalog.Revise(Uuid.CreateVersion4(), defined.Value.RequirementId, 2,
            Content("2027"), Actor, Now);
        var revised = catalog.Revise(Uuid.CreateVersion4(), defined.Value.RequirementId, 1,
            Content("Security awareness 2027"), Actor, Now);

        // Assert
        Assert.True(defined.IsSuccess);
        Assert.Equal("SAT", defined.Value.Identifier);
        Assert.Equal(defined.Value, retry.Value);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(duplicate.Error).Kind);
        Assert.False(stale.IsSuccess);
        Assert.Equal(2, revised.Value.Version);
        var first = catalog.Find(defined.Value.RequirementId, 1)!;
        Assert.Equal("Security awareness 2026", first.Content.CourseName);
        Assert.Equal(2, first.LatestVersion);
        Assert.Equal(TrainingCatalog.Cadence, first.Cadence);
        Assert.Equal(2, Assert.Single(catalog.Latest()).Version);
    }

    [Theory]
    [InlineData("", "manual")]
    [InlineData("Course", "connector")]
    public void ShouldRejectRequirementGivenMissingCourseOrUnsupportedSource(string course,
        string source)
    {
        // Arrange
        var catalog = new TrainingCatalog(TenantId, ProgramId);

        // Act
        var result = catalog.Define(Uuid.CreateVersion4(), "SAT", Content(course, source), Actor,
            Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }
}
