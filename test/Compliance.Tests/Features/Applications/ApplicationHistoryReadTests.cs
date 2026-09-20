using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationHistoryReadTests
{
    [Theory]
    [InlineData(1, "projection")]
    [InlineData(2, "source")]
    public async Task ShouldReportExactAndPagedHistoryLagGivenSourceRevision(
        long requestedRevision, string laggingLayer)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var source = new DeclaredApplication(tenantId, applicationId);
        Assert.True(source.Declare("Payroll", "Run payroll", null,
            Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow).IsSuccess);
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var consistency = new ApplicationHistoryReadConsistency(directory,
            new SourceReader(source));

        // Act
        var exact = await new GetApplicationRevisionHandler(directory, consistency).HandleAsync(
            new RequestContext<GetApplicationRevision>(new GetApplicationRevision(
                tenantId, applicationId, requestedRevision), new ClaimsPrincipal()),
            CancellationToken.None);
        var listed = await new ListApplicationRevisionsHandler(directory, consistency)
            .HandleAsync(new RequestContext<ListApplicationRevisions>(
                new ListApplicationRevisions(tenantId, applicationId,
                    MinimumApplicationRevision: requestedRevision), new ClaimsPrincipal()),
                CancellationToken.None);

        // Assert
        var exactError = Assert.IsType<RequestError>(exact.Error);
        var listError = Assert.IsType<RequestError>(listed.Error);
        Assert.Equal(RequestErrorKind.Conflict, exactError.Kind);
        Assert.Contains(laggingLayer, exactError.Message, StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.Conflict, listError.Kind);
        Assert.Contains(laggingLayer, listError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRejectInvalidRevisionAndUnknownApplicationGivenHistoryRead()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var consistency = new ApplicationHistoryReadConsistency(directory,
            new SourceReader(new DeclaredApplication(tenantId, applicationId)));
        var handler = new GetApplicationRevisionHandler(directory, consistency);

        // Act
        var invalid = await handler.HandleAsync(new RequestContext<GetApplicationRevision>(
            new GetApplicationRevision(tenantId, applicationId, 0), new ClaimsPrincipal()),
            CancellationToken.None);
        var unknown = await handler.HandleAsync(new RequestContext<GetApplicationRevision>(
            new GetApplicationRevision(tenantId, applicationId, 1), new ClaimsPrincipal()),
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(invalid.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(unknown.Error).Kind);
    }

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}
