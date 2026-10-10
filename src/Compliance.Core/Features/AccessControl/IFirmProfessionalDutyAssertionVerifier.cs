namespace Bdgrz.Compliance.Features.AccessControl;

interface IFirmProfessionalDutyAssertionVerifier
{
    bool TryVerify(string? signedAssertion, DateTimeOffset now,
        out VerifiedFirmProfessionalDutyAssertion assertion);
}
