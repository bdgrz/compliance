using System.Security.Claims;
using Bdgrz.Compliance.Features.Readiness;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Readiness;

public sealed class ReadinessProjectionReadTests
{
    [Fact]
    public async Task ShouldDelegateAssessmentListToReadModelGivenRequest()
    {
        // Arrange
        var readModel = new RecordingReadinessReadModel();
        var handler = new ListReadinessAssessmentsHandler(readModel);
        var request = new ListReadinessAssessments(Uuid.CreateVersion4(), Uuid.CreateVersion4());

        // Act
        var result = await handler.HandleAsync(new RequestContext<ListReadinessAssessments>(request,
            new ClaimsPrincipal()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Same(request, readModel.AssessmentListRequest);
    }

    sealed class RecordingReadinessReadModel : IReadinessReadModel
    {
        public ListReadinessAssessments? AssessmentListRequest { get; private set; }

        public ValueTask<Result<Page<ReadinessAssessmentSummaryView>>> ListAssessmentsAsync(
            ListReadinessAssessments request, CancellationToken ct)
        {
            AssessmentListRequest = request;
            return ValueTask.FromResult(Result<Page<ReadinessAssessmentSummaryView>>.Success(
                new Page<ReadinessAssessmentSummaryView>([], null)));
        }

        public ValueTask<Result<ReadinessAssessmentView>> GetAssessmentAsync(
            GetReadinessAssessment request, CancellationToken ct) =>
            throw new NotSupportedException();

        public ValueTask<Result<Page<ReadinessGapView>>> ListGapsAsync(ListReadinessGaps request,
            CancellationToken ct) => throw new NotSupportedException();

        public ValueTask<Result<Page<ReadinessAnnotationView>>> ListAnnotationsAsync(
            ListReadinessAnnotations request, CancellationToken ct) =>
            throw new NotSupportedException();

        public ValueTask<Result<Page<TypeIEntryDecisionView>>> ListTypeIEntryDecisionsAsync(
            ListTypeIEntryDecisions request, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
