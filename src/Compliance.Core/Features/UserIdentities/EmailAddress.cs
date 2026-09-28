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
    string _deliveryStatus = "not_issued";
    DateTimeOffset _expiresAt;
    Uuid? _recoveryChallengeId;
    string? _recoveryTokenHash;
    string? _recoveryTokenKeyId;
    DateTimeOffset _recoveryExpiresAt;
    string _recoveryDeliveryStatus = "not_issued";
    Uuid? _recoveryOldIdentityId;
    Uuid? _recoveryReplacementIdentityId;
    bool _recoveryCompleted;

    public EmailAddress(string address)
        : base(CreateId(address), new EventStreamAddress("bdgrz", "email-addresses", CreateId(address).ToString()))
    {
        _address = Normalize(address);
        On<EmailAddressReserved>(Apply);
        On<EmailChallengeIssued>(Apply);
        On<EmailChallengeDeliverySent>(Apply);
        On<EmailChallengeDeliveryFailed>(Apply);
        On<EmailAddressVerified>(Apply);
        On<IdentityRecoveryChallengeIssued>(Apply);
        On<IdentityRecoveryChallengeClaimed>(Apply);
        On<IdentityRecoveryChallengeCompleted>(Apply);
    }

    public bool IsVerified => _isVerified;
    public Uuid? Owner => _owner;
    public Uuid? CurrentChallengeId => _challengeId;
    public DateTimeOffset ChallengeExpiresAt => _expiresAt;
    public string DeliveryStatus => _deliveryStatus;
    public Uuid? CurrentRecoveryChallengeId => _recoveryChallengeId;
    public DateTimeOffset RecoveryChallengeExpiresAt => _recoveryExpiresAt;
    public string RecoveryDeliveryStatus => _recoveryDeliveryStatus;
    public Uuid? RecoveryOldIdentityId => _recoveryOldIdentityId;
    public Uuid? RecoveryReplacementIdentityId => _recoveryReplacementIdentityId;
    public bool IsRecoveryCompleted => _recoveryCompleted;

    public EmailChallengeStatusView GetChallengeStatus(DateTimeOffset now) =>
        new(_isVerified ? "verified" : _challengeId is null ? "not_issued" :
                now >= _expiresAt ? "expired" : _deliveryStatus,
            _challengeId is null ? null : _expiresAt);

    public Result IssueChallenge(Uuid userId, Uuid challengeId, string tokenHash, DateTimeOffset expiresAt,
        DateTimeOffset now, string? tokenKeyId = null)
    {
        if (_owner != userId)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden, "The email address is not owned by this user."));
        if (_isVerified)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The email address is already verified."));
        if (challengeId == Uuid.Empty || tokenHash.Length != 64 || expiresAt <= now)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A valid future challenge is required."));

        RaiseEvent(new EmailChallengeIssued(userId, _address, challengeId, tokenHash, expiresAt, tokenKeyId));
        return Result.Success;
    }

    public Result IssueRecoveryChallenge(Uuid userId, Uuid challengeId, string tokenHash,
        DateTimeOffset expiresAt, DateTimeOffset now, string tokenKeyId)
    {
        if (_owner != userId || !_isVerified)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Identity recovery requires an email address verified for this user."));
        if (challengeId == Uuid.Empty || tokenHash.Length != 64 || expiresAt <= now ||
            string.IsNullOrWhiteSpace(tokenKeyId))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A valid future recovery challenge is required."));
        if (_recoveryChallengeId is not null && !_recoveryCompleted &&
            now < _recoveryExpiresAt && _recoveryDeliveryStatus != "failed")
            return Result.Success;

        RaiseEvent(new IdentityRecoveryChallengeIssued(
            userId, _address, challengeId, tokenHash, expiresAt, tokenKeyId));
        return Result.Success;
    }

    public Result ClaimIdentityRecovery(Uuid userId, Uuid challengeId, string token,
        Uuid oldIdentityId, Uuid replacementIdentityId, DateTimeOffset now)
    {
        if (oldIdentityId == Uuid.Empty || replacementIdentityId == Uuid.Empty ||
            oldIdentityId == replacementIdentityId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Different old and replacement identities are required."));
        if (_owner != userId || !_isVerified || _recoveryChallengeId != challengeId)
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "No identity recovery challenge is available."));

        // The durable claim records the email proof for this exact pair. Let an OIDC-authenticated
        // retry resume after expiry; the handler still verifies replacement proof and ownership.
        if (_recoveryOldIdentityId is not null || _recoveryReplacementIdentityId is not null)
            return _recoveryOldIdentityId == oldIdentityId &&
                _recoveryReplacementIdentityId == replacementIdentityId
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The recovery challenge is already bound to another identity replacement."));

        var proof = ValidateIdentityRecoveryChallenge(userId, challengeId, token, now);
        if (!proof.IsSuccess)
            return Result.Failure(proof.Error);

        RaiseEvent(new IdentityRecoveryChallengeClaimed(
            userId, _address, challengeId, oldIdentityId, replacementIdentityId));
        return Result.Success;
    }

    public Result<Uuid> ValidateIdentityRecoveryChallenge(Uuid userId, Uuid challengeId,
        string token, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 64 ||
            token.Any(character => !Uri.IsHexDigit(character)))
            return Result<Uuid>.Failure(new RequestError(RequestErrorKind.Validation,
                "The identity recovery challenge is invalid."));
        if (_owner != userId || !_isVerified || _recoveryChallengeId != challengeId ||
            _recoveryTokenHash is null)
            return Result<Uuid>.Failure(new RequestError(RequestErrorKind.Forbidden,
                "No identity recovery challenge is available."));
        if (now >= _recoveryExpiresAt)
            return Result<Uuid>.Failure(new RequestError(RequestErrorKind.Validation,
                "The identity recovery challenge has expired."));

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var expectedHash = Convert.FromHexString(_recoveryTokenHash);
        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash)
            ? Result<Uuid>.Success(userId)
            : Result<Uuid>.Failure(new RequestError(RequestErrorKind.Validation,
                "The identity recovery challenge is invalid."));
    }

    public Result CompleteIdentityRecovery(Uuid userId, Uuid challengeId,
        Uuid oldIdentityId, Uuid replacementIdentityId, DateTimeOffset completedAt)
    {
        if (_owner != userId || _recoveryChallengeId != challengeId ||
            _recoveryOldIdentityId != oldIdentityId ||
            _recoveryReplacementIdentityId != replacementIdentityId)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The identity recovery challenge is not claimed for this replacement."));
        if (_recoveryCompleted)
            return Result.Success;

        RaiseEvent(new IdentityRecoveryChallengeCompleted(
            userId, _address, challengeId, oldIdentityId, replacementIdentityId, completedAt));
        return Result.Success;
    }

    public Result RecordDeliverySent(Uuid challengeId, DateTimeOffset sentAt)
    {
        if (_recoveryChallengeId == challengeId)
            return RecordRecoveryDeliverySent(challengeId, sentAt);
        if (_challengeId != challengeId || _isVerified || _deliveryStatus == "delivered")
            return Result.Success;
        if (_deliveryStatus == "failed")
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The failed email challenge must be reissued."));
        RaiseEvent(new EmailChallengeDeliverySent(challengeId, sentAt));
        return Result.Success;
    }

    public Result RecordDeliveryFailure(Uuid challengeId, string failureCode, DateTimeOffset failedAt)
    {
        if (_recoveryChallengeId == challengeId)
            return RecordRecoveryDeliveryFailure(challengeId, failureCode, failedAt);
        if (_challengeId != challengeId || _isVerified || _deliveryStatus is "failed" or "delivered")
            return Result.Success;
        if (failureCode is not ("delivery_failed" or "key_unavailable"))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An email delivery failure code is required."));
        RaiseEvent(new EmailChallengeDeliveryFailed(challengeId, failureCode, failedAt));
        return Result.Success;
    }

    public Result RecordRecoveryDeliverySent(Uuid challengeId, DateTimeOffset sentAt)
    {
        if (_recoveryChallengeId != challengeId || _recoveryDeliveryStatus == "delivered")
            return Result.Success;
        if (_recoveryDeliveryStatus == "failed")
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The failed identity recovery challenge must be reissued."));
        RaiseEvent(new EmailChallengeDeliverySent(challengeId, sentAt));
        return Result.Success;
    }

    public Result RecordRecoveryDeliveryFailure(Uuid challengeId, string failureCode, DateTimeOffset failedAt)
    {
        if (_recoveryChallengeId != challengeId || _recoveryDeliveryStatus is "failed" or "delivered")
            return Result.Success;
        if (failureCode is not ("delivery_failed" or "key_unavailable"))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An email delivery failure code is required."));
        RaiseEvent(new EmailChallengeDeliveryFailed(challengeId, failureCode, failedAt));
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
        _deliveryStatus = "pending";
    }

    void Apply(EmailChallengeDeliverySent sent)
    {
        if (_challengeId == sent.ChallengeId)
            _deliveryStatus = "delivered";
        else if (_recoveryChallengeId == sent.ChallengeId)
            _recoveryDeliveryStatus = "delivered";
        else
            throw new InvalidOperationException("The email delivery outcome is stale.");
    }

    void Apply(EmailChallengeDeliveryFailed failed)
    {
        if (_challengeId == failed.ChallengeId)
            _deliveryStatus = "failed";
        else if (_recoveryChallengeId == failed.ChallengeId)
            _recoveryDeliveryStatus = "failed";
        else
            throw new InvalidOperationException("The email delivery outcome is stale.");
    }

    void Apply(EmailAddressVerified verified)
    {
        if (_owner != verified.UserId || _challengeId != verified.ChallengeId ||
            !string.Equals(verified.EmailAddress, _address, StringComparison.Ordinal))
            throw new InvalidOperationException("The email verification does not match its owner and address.");
        _isVerified = true;
    }

    void Apply(IdentityRecoveryChallengeIssued issued)
    {
        if (_owner != issued.UserId || !_isVerified || issued.ChallengeId == Uuid.Empty ||
            issued.TokenHash.Length != 64 || issued.ExpiresAt <= DateTimeOffset.MinValue ||
            string.IsNullOrWhiteSpace(issued.TokenKeyId) ||
            !string.Equals(issued.EmailAddress, _address, StringComparison.Ordinal))
            throw new InvalidOperationException("The identity recovery challenge does not match its verified address.");

        _recoveryChallengeId = issued.ChallengeId;
        _recoveryTokenHash = issued.TokenHash;
        _recoveryTokenKeyId = issued.TokenKeyId;
        _recoveryExpiresAt = issued.ExpiresAt;
        _recoveryDeliveryStatus = "pending";
        _recoveryOldIdentityId = null;
        _recoveryReplacementIdentityId = null;
        _recoveryCompleted = false;
    }

    void Apply(IdentityRecoveryChallengeClaimed claimed)
    {
        if (_owner != claimed.UserId || !_isVerified || _recoveryChallengeId != claimed.ChallengeId ||
            _recoveryOldIdentityId is not null || _recoveryReplacementIdentityId is not null ||
            claimed.OldIdentityId == Uuid.Empty || claimed.ReplacementIdentityId == Uuid.Empty ||
            claimed.OldIdentityId == claimed.ReplacementIdentityId ||
            !string.Equals(claimed.EmailAddress, _address, StringComparison.Ordinal))
            throw new InvalidOperationException("The identity recovery claim does not match its challenge.");

        _recoveryOldIdentityId = claimed.OldIdentityId;
        _recoveryReplacementIdentityId = claimed.ReplacementIdentityId;
    }

    void Apply(IdentityRecoveryChallengeCompleted completed)
    {
        if (_owner != completed.UserId || _recoveryChallengeId != completed.ChallengeId ||
            _recoveryOldIdentityId != completed.OldIdentityId ||
            _recoveryReplacementIdentityId != completed.ReplacementIdentityId || _recoveryCompleted ||
            !string.Equals(completed.EmailAddress, _address, StringComparison.Ordinal))
            throw new InvalidOperationException("The identity recovery completion does not match its claim.");

        _recoveryCompleted = true;
    }

    static Uuid CreateId(string address) => Uuid.CreateVersion5(AddressNamespaceId, Normalize(address));

    static string Normalize(string address) => EmailAddresses.TryNormalize(address, out var normalized)
        ? normalized
        : throw new ArgumentException("A valid email address is required.", nameof(address));
}
