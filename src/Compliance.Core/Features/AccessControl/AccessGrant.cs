using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public sealed class AccessGrant : Aggregate
{
    readonly Uuid _tenantId;
    AccessGrantTerms? _terms;
    Uuid? _membershipEpisodeId;
    bool _isRevoked;

    public bool IsRevoked => _isRevoked;
    public Uuid? MembershipEpisodeId => _membershipEpisodeId;

    public AccessGrant(Uuid tenantId, Uuid grantId)
        : base(grantId, new EventStreamAddress(tenantId.ToString(), "access-grants", grantId.ToString()))
    {
        _tenantId = tenantId;
        On<AccessGrantIssued>(ev =>
        {
            _terms = ev.Terms;
            _membershipEpisodeId = ev.MembershipEpisodeId;
        });
        On<AccessGrantRevoked>(_ => _isRevoked = true);
    }

    public Result Issue(AccessGrantTerms terms, Uuid? membershipEpisodeId = null)
    {
        var validation = Validate(terms);
        if (validation is not null)
            return Result.Failure(validation);

        if (_terms is not null)
            return _isRevoked
                ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "A revoked access grant cannot be reissued."))
                : _terms == terms && _membershipEpisodeId == membershipEpisodeId
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The access grant already has different terms."));

        RaiseEvent(new AccessGrantIssued(_tenantId, Id, terms, membershipEpisodeId));
        return Result.Success;
    }

    public Result Revoke(ActorReference revokedBy, DateTimeOffset revokedAt)
    {
        if (_terms is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The access grant was not found."));
        if (_isRevoked)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The access grant is already revoked."));
        if (!IsValidActor(revokedBy) || revokedAt == default)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A grant revocation requires an actor and timestamp."));

        RaiseEvent(new AccessGrantRevoked(_tenantId, Id, revokedBy, revokedAt));
        return Result.Success;
    }

    RequestError? Validate(AccessGrantTerms terms)
    {
        if (terms is null || terms.Principal is null || terms.Scope is null || terms.Source is null)
            return new RequestError(RequestErrorKind.Validation, "An access grant requires complete terms.");
        if (terms.Principal.Id == Uuid.Empty || !Enum.IsDefined(terms.Principal.Kind) ||
            terms.RoleId == Uuid.Empty || terms.Scope.Id == Uuid.Empty ||
            !Enum.IsDefined(terms.Scope.Kind) || !IsValidActor(terms.GrantedBy) ||
            string.IsNullOrWhiteSpace(terms.Source.Kind) || string.IsNullOrWhiteSpace(terms.Source.Id) ||
            terms.EffectiveFrom == default ||
            terms.EffectiveUntil is { } until && until <= terms.EffectiveFrom)
            return new RequestError(RequestErrorKind.Validation, "The access grant terms are invalid.");
        if (terms.Scope.Kind == AccessGrantScopeKind.Organization && terms.Scope.Id != _tenantId)
            return new RequestError(RequestErrorKind.Validation, "An organization scope requires an organization id.");
        if (terms.Scope.Kind == AccessGrantScopeKind.SharedResource &&
            string.IsNullOrWhiteSpace(terms.Scope.ResourceType))
            return new RequestError(RequestErrorKind.Validation,
                "A shared resource scope requires its resource type.");
        if (terms.Scope.Kind != AccessGrantScopeKind.SharedResource && terms.Scope.ResourceType is not null)
            return new RequestError(RequestErrorKind.Validation,
                "Only a shared resource scope may specify a resource type.");
        return null;
    }

    static bool IsValidActor(ActorReference? actor) =>
        actor is { Kind: "member" } &&
        !string.IsNullOrWhiteSpace(actor.Id) && !string.IsNullOrWhiteSpace(actor.Display);
}
