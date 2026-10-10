using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class SignedFirmProfessionalDutyAssertionVerifierTests
{
    [Fact]
    public void ShouldRejectAssertionGivenNoConfiguredTrustedIssuer()
    {
        // Arrange
        using var provider = Compose(configureTrust: false);
        using var scope = provider.CreateScope();
        var verifier = scope.ServiceProvider.GetRequiredService<IFirmProfessionalDutyAssertionVerifier>();
        var now = CurrentSecond();
        var signed = Issue(now);

        // Act
        var accepted = verifier.TryVerify(signed.Token, now, out _);

        // Assert
        Assert.False(accepted);
    }

    [Fact]
    public void ShouldVerifyConfiguredSignedAssertionGivenConfiguredTrustedIssuer()
    {
        // Arrange
        using var provider = Compose(configureTrust: true);
        using var scope = provider.CreateScope();
        var verifier = scope.ServiceProvider.GetRequiredService<IFirmProfessionalDutyAssertionVerifier>();
        var now = CurrentSecond();
        var signed = Issue(now);

        // Act
        var accepted = verifier.TryVerify(signed.Token, now, out var evidence);

        // Assert
        Assert.True(accepted);
        Assert.Equal(signed.Evidence.Issuer, evidence.Issuer);
        Assert.Equal(signed.Evidence.Audience, evidence.Audience);
        Assert.Equal(signed.Evidence.AssertionId, evidence.AssertionId);
        Assert.Equal(signed.Evidence.DesignationId, evidence.DesignationId);
        Assert.Equal(signed.Evidence.UserId, evidence.UserId);
        Assert.Equal(signed.Evidence.Duty, evidence.Duty);
        Assert.Equal(signed.Evidence.TenantId, evidence.TenantId);
        Assert.Equal(signed.Evidence.AuthorityRevision, evidence.AuthorityRevision);
        Assert.Equal(signed.Evidence.ExpiresAt, evidence.ExpiresAt);
        Assert.Equal(signed.Evidence.EvidenceDigest, evidence.EvidenceDigest);
    }

    [Fact]
    public void ShouldRejectSignedAssertionsGivenInvalidIssuerClaimsOrSignature()
    {
        // Arrange
        using var provider = Compose(configureTrust: true);
        using var scope = provider.CreateScope();
        var verifier = scope.ServiceProvider.GetRequiredService<IFirmProfessionalDutyAssertionVerifier>();
        var now = CurrentSecond();
        var valid = Claims(now);
        var wrongIssuer = Copy(valid);
        wrongIssuer["iss"] = "https://other-authority.example";
        var wrongAudience = Copy(valid);
        wrongAudience["aud"] = "other-audience";
        var wrongSubject = Copy(valid);
        wrongSubject["sub"] = "not-a-canonical-user";
        var wrongDutyScope = Copy(valid);
        wrongDutyScope["duty"] = FirmProfessionalDuty.RuleRatifier;
        var unsupportedClaim = Copy(valid);
        unsupportedClaim["platform_operator"] = true;
        var invalidRevision = Copy(valid);
        invalidRevision["authority_revision"] = 0;
        var invalidStatus = Copy(valid);
        invalidStatus["status"] = "draft";
        var expired = ProfessionalDutyAssertionTestIssuer.Claims(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            FirmProfessionalDuty.RuleRatifier, null, 1, "active", null, now.AddHours(-1), now.AddMinutes(-1));
        var future = ProfessionalDutyAssertionTestIssuer.Claims(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            FirmProfessionalDuty.RuleRatifier, null, 1, "active", null, now.AddMinutes(1), now.AddMinutes(20));
        var tooLong = ProfessionalDutyAssertionTestIssuer.Claims(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            FirmProfessionalDuty.RuleRatifier, null, 1, "active", null, now.AddHours(-2), now.AddMinutes(20));
        var validToken = ProfessionalDutyAssertionTestIssuer.SignClaims(valid);
        var signatureStart = validToken.LastIndexOf('.') + 1;
        var tamperedToken = validToken[..signatureStart] +
            (validToken[signatureStart] == 'A' ? 'B' : 'A') + validToken[(signatureStart + 1)..];
        var invalidTokens = new[]
        {
            ProfessionalDutyAssertionTestIssuer.SignClaims(wrongIssuer),
            ProfessionalDutyAssertionTestIssuer.SignClaims(wrongAudience),
            ProfessionalDutyAssertionTestIssuer.SignClaims(wrongSubject),
            ProfessionalDutyAssertionTestIssuer.SignClaims(wrongDutyScope),
            ProfessionalDutyAssertionTestIssuer.SignClaims(unsupportedClaim),
            ProfessionalDutyAssertionTestIssuer.SignClaims(invalidRevision),
            ProfessionalDutyAssertionTestIssuer.SignClaims(invalidStatus),
            ProfessionalDutyAssertionTestIssuer.SignClaims(expired),
            ProfessionalDutyAssertionTestIssuer.SignClaims(future),
            ProfessionalDutyAssertionTestIssuer.SignClaims(tooLong),
            ProfessionalDutyAssertionTestIssuer.SignClaims(valid, keyId: "untrusted-key"),
            ProfessionalDutyAssertionTestIssuer.SignClaims(valid, algorithm: "none"),
            tamperedToken
        };

        // Act
        var results = invalidTokens.Select(token => verifier.TryVerify(token, now, out _)).ToArray();

        // Assert
        Assert.All(results, Assert.False);
    }

    [Fact]
    public void ShouldRejectPartialTrustedIssuerConfigurationGivenMissingKeyMaterial()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Compliance:ProfessionalAuthority:Issuer"] = ProfessionalDutyAssertionTestIssuer.Issuer
        }).Build();
        var services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() => services.AddCompliance(configuration));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    static ServiceProvider Compose(bool configureTrust)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance"
        };
        if (configureTrust)
            ProfessionalDutyAssertionTestIssuer.AddConfiguration(settings);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddCompliance(configuration, developerAuthentication: true);
        return services.BuildServiceProvider();
    }

    static DateTimeOffset CurrentSecond() => DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    static ProfessionalDutyAssertionTestIssuer.SignedAssertion Issue(DateTimeOffset now) =>
        ProfessionalDutyAssertionTestIssuer.Issue(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            FirmProfessionalDuty.RuleRatifier, null, 1, "active", null, now.AddMinutes(-1), now.AddMinutes(20));

    static Dictionary<string, object> Claims(DateTimeOffset now) =>
        ProfessionalDutyAssertionTestIssuer.Claims(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            FirmProfessionalDuty.EngagementPartner, Uuid.CreateVersion4(), 1, "active", null,
            now.AddMinutes(-1), now.AddMinutes(20));

    static Dictionary<string, object> Copy(Dictionary<string, object> claims) => new(claims);
}
