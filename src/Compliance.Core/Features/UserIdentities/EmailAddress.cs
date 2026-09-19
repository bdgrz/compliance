using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Owns the one-user reservation for a normalized email address.</summary>
public sealed class EmailAddress : Aggregate
{
    static readonly Uuid AddressNamespaceId =
        Uuid.Parse("2c581f32-1b42-5928-8bc1-e604d4a9cd58", CultureInfo.InvariantCulture);

    readonly string _address;
    Uuid? _owner;
    bool _isVerified;
    Uuid? _challengeId;
    string? _tokenHash;
    DateTimeOffset _expiresAt;

    public EmailAddress(string address)
        : base(CreateId(address), new EventStreamAddress("bdgrz", "email-addresses", CreateId(address).ToString()))
    {
        _address = Normalize(address);
        On<EmailAddressReserved>(Apply);
        On<EmailChallengeIssued>(Apply);
        On<EmailAddressVerified>(Apply);
    }

    public bool IsVerified => _isVerified;

    public Result IssueChallenge(Uuid userId, Uuid challengeId, string tokenHash, DateTimeOffset expiresAt,
        DateTimeOffset now)
    {
        if (_owner != userId)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The email address is not owned by this user."));
        if (_isVerified)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The email address is already verified."));
        if (challengeId == Uuid.Empty || tokenHash.Length != 64 || expiresAt <= now)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A valid future challenge is required."));

        RaiseEvent(new EmailChallengeIssued(userId, _address, challengeId, tokenHash, expiresAt));
        return Result.Success;
    }

    public Result CompleteChallenge(Uuid userId, string token, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A verification challenge is required."));
        if (_owner != userId || _challengeId is null || _tokenHash is null)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "No verification challenge is available."));
        if (now >= _expiresAt)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The verification challenge has expired."));

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var expectedHash = Convert.FromHexString(_tokenHash);
        if (!CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The verification challenge is invalid."));

        if (!_isVerified)
            RaiseEvent(new EmailAddressVerified(userId, _address, _challengeId.Value));
        return Result.Success;
    }

    public Result Reserve(Uuid userId)
    {
        if (userId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "An email owner is required."));

        if (_owner is null)
            RaiseEvent(new EmailAddressReserved(userId, _address));
        else if (_owner != userId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The email address belongs to another user."));

        return Result.Success;
    }

    void Apply(EmailAddressReserved reserved)
    {
        if (_owner is not null || reserved.UserId == Uuid.Empty ||
            !string.Equals(reserved.EmailAddress, _address, StringComparison.Ordinal))
            throw new InvalidOperationException("The email reservation does not match its address.");
        _owner = reserved.UserId;
    }

    void Apply(EmailChallengeIssued issued)
    {
        if (_owner != issued.UserId || _isVerified ||
            !string.Equals(issued.EmailAddress, _address, StringComparison.Ordinal))
            throw new InvalidOperationException("The email challenge does not match its owner and address.");
        _challengeId = issued.ChallengeId;
        _tokenHash = issued.TokenHash;
        _expiresAt = issued.ExpiresAt;
    }

    void Apply(EmailAddressVerified verified)
    {
        if (_owner != verified.UserId || _challengeId != verified.ChallengeId ||
            !string.Equals(verified.EmailAddress, _address, StringComparison.Ordinal))
            throw new InvalidOperationException("The email verification does not match its owner and address.");
        _isVerified = true;
    }

    static Uuid CreateId(string address) => Uuid.CreateVersion5(AddressNamespaceId, Normalize(address));

    static string Normalize(string address) => EmailAddresses.TryNormalize(address, out var normalized)
        ? normalized
        : throw new ArgumentException("A valid email address is required.", nameof(address));
}
