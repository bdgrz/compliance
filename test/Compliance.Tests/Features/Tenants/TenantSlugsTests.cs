namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantSlugsTests
{
    [Fact]
    public void ShouldExplainRejectionGivenInvalidSlug()
    {
        // Arrange
        const string tooShort = "abc";
        const string reservedApi = "api";
        const string reserved = "developer-login";
        const string doubleHyphen = "bad--slug";

        // Act
        var shortAccepted = TenantSlugs.TryNormalize(tooShort, out _, out var shortReason);
        var apiAccepted = TenantSlugs.TryNormalize(reservedApi, out _, out _);
        var reservedAccepted = TenantSlugs.TryNormalize(reserved, out _, out var reservedReason);
        var hyphenAccepted = TenantSlugs.TryNormalize(doubleHyphen, out _, out var hyphenReason);

        // Assert
        Assert.False(shortAccepted);
        Assert.Contains("4 to 63", shortReason, StringComparison.Ordinal);
        Assert.False(apiAccepted);
        Assert.False(reservedAccepted);
        Assert.Contains("reserved route", reservedReason, StringComparison.Ordinal);
        Assert.False(hyphenAccepted);
        Assert.Contains("consecutive hyphens", hyphenReason, StringComparison.Ordinal);
    }
    [Theory]
    [InlineData("acme")]
    [InlineData("acme-corp")]
    [InlineData("a2z9")]
    public void ShouldAcceptGivenValidSlug(string slug) =>
        Assert.True(TenantSlugs.TryNormalize(slug, out _));

    [Fact]
    public void ShouldLowercaseAndTrimGivenMixedCaseSlug() =>
        Assert.True(TenantSlugs.TryNormalize("  ACME  ", out var normalized) && normalized == "acme");

    [Theory]
    [InlineData("abc")] // shorter than 4
    [InlineData("1acme")] // starts with a digit
    [InlineData("-acme")] // starts with a hyphen
    [InlineData("acme-")] // ends with a hyphen
    [InlineData("ac--me")] // consecutive hyphens
    [InlineData("acme!")] // invalid character
    public void ShouldRejectGivenMalformedSlug(string slug) =>
        Assert.False(TenantSlugs.TryNormalize(slug, out _));

    [Fact]
    public void ShouldRejectGivenOversizedSlug() =>
        Assert.False(TenantSlugs.TryNormalize(new string('a', 64), out _));

    [Theory]
    [InlineData("api")]
    [InlineData("auth")]
    [InlineData("tenants")]
    [InlineData("organizations")]
    [InlineData("login")]
    public void ShouldRejectGivenReservedRoute(string slug) =>
        Assert.False(TenantSlugs.TryNormalize(slug, out _));

    [Fact]
    public void ShouldRejectGivenTenantIdShapedSlug() =>
        Assert.False(TenantSlugs.TryNormalize("ee6c0952-f147-4aa4-8b65-f57619e3843d", out _));
}
