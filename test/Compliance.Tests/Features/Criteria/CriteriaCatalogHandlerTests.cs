using System.Security.Claims;
using Bdgrz.Compliance.Features.Criteria;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Criteria;

public sealed class CriteriaCatalogHandlerTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly ClaimsPrincipal Actor = new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
        "BdgrzSession"));
    static readonly Uuid EditionId = CriteriaCatalog.Platform.Edition.EditionId;

    [Fact]
    public async Task ShouldWalkFilteredPagesAndRejectTransplantedCursorGivenOtherTenant()
    {
        // Arrange
        var handler = new ListCriteriaCatalogEntriesHandler(CriteriaCatalog.Platform);
        var request = new ListCriteriaCatalogEntries(TenantId, EditionId, "security",
            "criterion", Limit: 10);

        // Act
        var identifiers = new List<string>();
        string? firstCursor = null;
        string? cursor = null;
        do
        {
            var page = await ListAsync(handler, request with { Cursor = cursor });
            Assert.True(page.IsSuccess);
            identifiers.AddRange(page.Value!.Items.Select(static entry => entry.Identifier));
            cursor = page.Value.NextCursor;
            firstCursor ??= cursor;
        } while (cursor is not null);
        var otherTenant = await ListAsync(handler, request with
        {
            TenantId = Uuid.CreateVersion4(),
            Cursor = firstCursor,
        });
        var otherFilter = await ListAsync(handler, request with
        {
            Category = "privacy",
            Cursor = firstCursor,
        });

        // Assert
        Assert.Equal(33, identifiers.Count);
        Assert.Equal(identifiers.Count, identifiers.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(RequestErrorKind.Validation, otherTenant.Error?.Kind);
        Assert.Equal(RequestErrorKind.Validation, otherFilter.Error?.Kind);
    }

    [Theory]
    [InlineData("governance", null, 50)]
    [InlineData(null, "control", 50)]
    [InlineData(null, null, 0)]
    [InlineData(null, null, 201)]
    public async Task ShouldRejectListGivenUnknownFilterOrLimit(string? category, string? kind,
        int limit)
    {
        // Arrange
        var handler = new ListCriteriaCatalogEntriesHandler(CriteriaCatalog.Platform);

        // Act
        var result = await ListAsync(handler, new ListCriteriaCatalogEntries(TenantId, EditionId,
            category, kind, Limit: limit));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenUnknownEditionOrIdentifier()
    {
        // Arrange
        var entries = new GetCriteriaCatalogEntryHandler(CriteriaCatalog.Platform);
        var editions = new GetCriteriaCatalogEditionHandler(CriteriaCatalog.Platform);

        // Act
        var known = await entries.HandleAsync(new RequestContext<GetCriteriaCatalogEntry>(
            new GetCriteriaCatalogEntry(TenantId, EditionId, "CC6.1"), Actor),
            CancellationToken.None);
        var unknownIdentifier = await entries.HandleAsync(new RequestContext<GetCriteriaCatalogEntry>(
            new GetCriteriaCatalogEntry(TenantId, EditionId, "CC6.9"), Actor),
            CancellationToken.None);
        var unknownEdition = await editions.HandleAsync(new RequestContext<GetCriteriaCatalogEdition>(
            new GetCriteriaCatalogEdition(TenantId, Uuid.CreateVersion4()), Actor),
            CancellationToken.None);

        // Assert
        Assert.Equal("CC6.1", known.Value?.SourceIdentifier);
        Assert.Equal(RequestErrorKind.NotFound, unknownIdentifier.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, unknownEdition.Error?.Kind);
    }

    static async Task<Result<Page<Criterion>>> ListAsync(ListCriteriaCatalogEntriesHandler handler,
        ListCriteriaCatalogEntries request) =>
        await handler.HandleAsync(new RequestContext<ListCriteriaCatalogEntries>(request, Actor),
            CancellationToken.None);
}
