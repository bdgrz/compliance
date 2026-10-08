using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>Preserves personal acknowledgement while applying retained Attest history to management proxy recording.</summary>
public sealed class PolicyAcknowledgementRecorderGuard(IAggregateReader reader,
    ClientManagementIndependenceGuard independence)
{
    public async ValueTask<PolicyAcknowledgementPersonSnapshot> CaptureAsync(Uuid tenantId, Uuid personId,
        CancellationToken ct = default)
    {
        var person = await reader.HydrateAsync(new Person(tenantId, personId), ct).ConfigureAwait(false);
        return PolicyAcknowledgementPersonSnapshot.Capture(person);
    }

    public async ValueTask<PolicyAcknowledgementRecorderEvaluation> EvaluateAsync(Uuid tenantId, Uuid canonicalUserId,
        Uuid personId, CancellationToken ct = default) =>
        await EvaluateCapturedAsync(tenantId, canonicalUserId,
            await CaptureAsync(tenantId, personId, ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    /// <summary>Consumes the caller's one authoritative capture without rehydrating its correlation.</summary>
    public async ValueTask<PolicyAcknowledgementRecorderEvaluation> EvaluateCapturedAsync(Uuid tenantId,
        Uuid canonicalUserId, PolicyAcknowledgementPersonSnapshot person, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(person);
        if (tenantId == Uuid.Empty || canonicalUserId == Uuid.Empty || person.TenantId != tenantId || person.PersonId == Uuid.Empty)
            return new(person, false, false);
        var self = person.CorrelatedUserId == canonicalUserId;
        if (self || person.CorrelatedUserId is not null)
            return new(person, self, false);
        return new(person, false,
            await independence.CanAuthorAsync(tenantId, canonicalUserId, ct).ConfigureAwait(false));
    }
}
