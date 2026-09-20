using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>One replaceable, single-use invitation per tenant and normalized email address.</summary>
public sealed class TenantInvitation : Aggregate
{
    static readonly Uuid InvitationNamespaceId = Uuid.Parse(
        "d769ee6a-18e9-529e-a5a5-46082e96db53", CultureInfo.InvariantCulture);

    readonly Uuid _tenantId;
    readonly string _emailAddress;
    string? _tokenHash;
    DateTimeOffset _expiresAt;
    string? _affiliation;
    bool _administrator;
    string? _builtInRole;
    bool _accepted;
    Uuid _acceptedUserId;

    public TenantInvitation(Uuid tenantId, string emailAddress)
        : base(CreateId(tenantId, emailAddress),
            new EventStreamAddress(tenantId.ToString(), "tenant-invitations",
                CreateId(tenantId, emailAddress).ToString()))
    {
        _tenantId = tenantId;
        _emailAddress = Normalize(emailAddress);
        On<TenantMemberInvited>(Apply);
        On<TenantInvitationAccepted>(accepted =>
        {
            _accepted = true;
            _acceptedUserId = accepted.UserId;
        });
    }

    public Result Invite(string affiliation, bool administrator, string tokenHash,
        DateTimeOffset expiresAt, DateTimeOffset now, Uuid invitedBy,
        string? builtInRole = null)
    {
        if (affiliation is not ("client_personnel" or "firm_staff"))
            return Failure(RequestErrorKind.Validation, "Affiliation must be client_personnel or firm_staff.");
        if (administrator && affiliation != "client_personnel")
            return Failure(RequestErrorKind.Validation, "The first administrator must be client personnel.");
        if (administrator && builtInRole is not null)
            return Failure(RequestErrorKind.Validation,
                "A first-administrator invitation cannot select a second role.");
        if (builtInRole is not null &&
            (affiliation != "client_personnel" || BuiltInRbac.TeamIdForRole(_tenantId, builtInRole) is null))
            return Failure(RequestErrorKind.Validation,
                "A built-in role requires client personnel and a supported role value.");
        if (_accepted)
            return Failure(RequestErrorKind.Conflict, "The invitation has already been accepted.");
        if (_affiliation is not null &&
            (_affiliation != affiliation || _administrator != administrator))
            return Failure(RequestErrorKind.Conflict,
                "The pending invitation has a different affiliation or administrator purpose.");
        if (tokenHash.Length != 64 || expiresAt <= now || invitedBy == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A valid invitation is required.");

        RaiseEvent(new TenantMemberInvited(_tenantId, _emailAddress, affiliation,
            administrator, tokenHash, expiresAt, invitedBy, builtInRole));
        return Result.Success;
    }

    public Result Accept(Uuid userId, string token, DateTimeOffset now)
    {
        if (_accepted)
            return _acceptedUserId == userId
                ? Result.Success
                : Failure(RequestErrorKind.Forbidden, "The invitation belongs to another user.");
        if (userId == Uuid.Empty || string.IsNullOrWhiteSpace(token) || _tokenHash is null)
            return Failure(RequestErrorKind.Validation, "A valid invitation token is required.");
        if (now >= _expiresAt)
            return Failure(RequestErrorKind.Validation, "The invitation has expired.");

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var expectedHash = Convert.FromHexString(_tokenHash);
        if (!CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash))
            return Failure(RequestErrorKind.Validation, "The invitation token is invalid.");

        RaiseEvent(new TenantInvitationAccepted(_tenantId, userId, _emailAddress,
            _affiliation!, _administrator, _builtInRole));
        return Result.Success;
    }

    void Apply(TenantMemberInvited invited)
    {
        if (invited.TenantId != _tenantId || invited.EmailAddress != _emailAddress)
            throw new InvalidOperationException("The invitation does not match its tenant and email address.");
        _tokenHash = invited.TokenHash;
        _expiresAt = invited.ExpiresAt;
        _affiliation = invited.Affiliation;
        _administrator = invited.Administrator;
        _builtInRole = invited.BuiltInRole;
    }

    static Uuid CreateId(Uuid tenantId, string emailAddress) =>
        Uuid.CreateVersion5(InvitationNamespaceId, $"{tenantId}\n{Normalize(emailAddress)}");

    static string Normalize(string emailAddress) =>
        EmailAddresses.TryNormalize(emailAddress, out var normalized)
            ? normalized
            : throw new ArgumentException("A valid email address is required.", nameof(emailAddress));

    static Result Failure(RequestErrorKind kind, string message) =>
        Result.Failure(new RequestError(kind, message));
}
