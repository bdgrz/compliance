using System.Collections.Frozen;

namespace Bdgrz.Compliance.Features.Applications;

public sealed partial class DeclaredApplication
{
    internal void ResolveImportVisibility(ApplicationImportLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        foreach (var effect in _pendingImportEffects.Values)
        {
            if (ledger.Stream.Realm != _tenantId.ToString() || ledger.Id != ApplicationImportLedger.IdFor(
                    _tenantId, effect.Plan.SourceKey, effect.Plan.SourceNamespace))
                continue;
            if (ledger.IsEffectCommitted(effect))
            {
                ApplyCommittedImportCreation(effect);
                _settledImportBatches = _settledImportBatches.Append(effect.Plan.BatchId).ToFrozenSet();
            }
            else if (ledger.IsCancellationDurable(effect.Plan.BatchId))
                _settledImportBatches = _settledImportBatches.Append(effect.Plan.BatchId).ToFrozenSet();
        }
    }

    void ApplyCommittedImportCreation(ApplicationImportEffectPending effect)
    {
        if (effect.Row.Decision != "create_new" || _created)
            return;
        _created = true;
        _initialName = effect.Row.Name.Trim();
        _initialPurpose = effect.Row.Purpose.Trim();
        _initialOwnerReference = NormalizeOptional(effect.Row.OwnerReference);
        _revision = Math.Max(1, _revision);
    }
}
