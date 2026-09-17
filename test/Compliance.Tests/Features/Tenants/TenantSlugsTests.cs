namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class TenantSlugsTests
{
    [Theory]
    [InlineData("acme")]
    [InlineData("acme-corp")]
    [InlineData("a2z9")]
    public void TryNormalizeShouldAcceptAValidSlug(string slug) =>
        Assert.True(TenantSlugs.TryNormalize(slug, out _));

    [Fact]
    public void TryNormalizeShouldLowercaseAndTrim() =>
        Assert.True(TenantSlugs.TryNormalize("  ACME  ", out var normalized) && normalized == "acme");

    [Theory]
    [InlineData("abc")] // shorter than 4
    [InlineData("1acme")] // starts with a digit
    [InlineData("-acme")] // starts with a hyphen
    [InlineData("acme-")] // ends with a hyphen
    [InlineData("ac--me")] // consecutive hyphens
    [InlineData("acme!")] // invalid character
    public void TryNormalizeShouldRejectAMalformedSlug(string slug) =>
        Assert.False(TenantSlugs.TryNormalize(slug, out _));

    [Fact]
    public void TryNormalizeShouldRejectASlugLongerThan63Characters() =>
        Assert.False(TenantSlugs.TryNormalize(new string('a', 64), out _));

    [Theory]
    [InlineData("api")]
    [InlineData("auth")]
    [InlineData("tenants")]
    [InlineData("organizations")]
    [InlineData("login")]
    public void TryNormalizeShouldRejectAReservedRoute(string slug) =>
        Assert.False(TenantSlugs.TryNormalize(slug, out _));

    [Fact]
    public void TryNormalizeShouldRejectASlugShapedLikeATenantId() =>
        Assert.False(TenantSlugs.TryNormalize("ee6c0952-f147-4aa4-8b65-f57619e3843d", out _));
}
