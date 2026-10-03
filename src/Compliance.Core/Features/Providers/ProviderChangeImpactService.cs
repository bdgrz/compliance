using System.Security.Cryptography;
using System.Text.Json;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.TechnologyInventory;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Providers;

/// <summary>
/// Builds a bounded view of provider-related systems, data, controls, evidence and scope.
/// This is a planning preview only; it does not approve a supplier decision or claim a
/// cross-stream transaction.
/// </summary>
public sealed class ProviderChangeImpactService(IAggregateReader reader,
    ProviderReferences references, ITechnologyInventoryReader inventory,
    TechnologyInventoryReadConsistency inventoryConsistency,
    IControlDraftDirectoryReader controls, ControlDraftListReadConsistency controlConsistency,
    IBoundaryDirectoryReader boundaries, IProgramDirectoryReader programs,
    ProgramSetupWorkReadConsistency programConsistency)
{
    const int PageSize = 200;
    const int MaximumScannedRecords = 1000;
    const int MaximumReturnedRecords = 200;
    static readonly string[] ContextNames = ["systems", "data", "controls", "evidence", "scope"];

    public async ValueTask<Result<ProviderChangeImpactPreview>> PreviewAsync(
        PreviewProviderChange request, CancellationToken ct)
    {
        if (request.ExpectedRevision < 1 || request.EffectiveOn == default ||
            request.ChangeKind is not ("renewal" or "material_change" or "termination") ||
            string.IsNullOrWhiteSpace(request.ChangeSummary) || request.ChangeSummary.Trim().Length > 2000)
            return Failure(RequestErrorKind.Validation,
                "A provider change requires a positive expected revision, a supported change kind, an effective date, and a summary of at most 2000 characters.");

        var providerRegister = await reader.HydrateAsync(new ProviderRegister(request.TenantId), ct)
            .ConfigureAwait(false);
        var provider = providerRegister.Get(request.ProviderId);
        if (provider is null || provider.TenantId != request.TenantId)
            return Failure(RequestErrorKind.NotFound, "The provider was not found.");
        if (provider.Revision != request.ExpectedRevision)
            return Result<ProviderChangeImpactPreview>.Failure(
                VersionedRecordRules.StaleRevision("provider", provider.Revision).ToRequestError());

        var resolved = await references.ResolveAsync(request.TenantId, provider.Content, ct)
            .ConfigureAwait(false);
        if (!resolved.IsSuccess)
            return Result<ProviderChangeImpactPreview>.Failure(resolved.Error);

        var programFence = await programConsistency.CaptureAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!programFence.IsSuccess)
            return Result<ProviderChangeImpactPreview>.Failure(programFence.Error);

        var records = ContextNames.ToDictionary(static name => name,
            static _ => new List<ProviderChangeAffectedRecord>(), StringComparer.Ordinal);
        var incomplete = ContextNames.ToDictionary(static name => name,
            static _ => (string?)null, StringComparer.Ordinal);
        var content = resolved.Value;
        var dependencies = content.Dependencies ?? [];
        if (dependencies.Any(static dependency => dependency.SubjectId is null))
            foreach (var name in ContextNames)
                incomplete[name] = "unresolved_provider_dependency";

        var applicationIds = new HashSet<Uuid>();
        var instanceIds = new HashSet<Uuid>();
        var serviceIds = new HashSet<Uuid>();
        var programIds = new HashSet<Uuid>();
        var impactedComponentIds = new HashSet<Uuid>();
        var impactedInformationIds = new HashSet<Uuid>();
        var impactedDataFlowIds = new HashSet<Uuid>();

        foreach (var dependency in dependencies)
        {
            var affectedByChange = IsAffectedByChange(dependency, request.ChangeKind,
                request.EffectiveOn);
            if (dependency.ProgramId is { } programId)
            {
                if (affectedByChange)
                    programIds.Add(programId);
                Add("scope", "program", programId, null, dependency.ProgramRevision,
                    DependencyRelationship(request.ChangeKind, affectedByChange));
            }
            if (dependency.SubjectId is not { } subjectId)
                continue;
            var relationship = DependencyRelationship(request.ChangeKind, affectedByChange);
            if (dependency.SubjectKind == "client_service")
            {
                if (affectedByChange)
                    serviceIds.Add(subjectId);
                Add("systems", "client_service", subjectId, dependency.ProgramId,
                    dependency.SourceRevision, relationship);
                Add("scope", "client_service", subjectId, dependency.ProgramId,
                    dependency.SourceRevision, relationship);
            }
            else if (dependency.SubjectKind == "system_instance")
            {
                var applicationId = dependency.ApplicationId!.Value;
                if (affectedByChange)
                {
                    instanceIds.Add(subjectId);
                    applicationIds.Add(applicationId);
                }
                Add("systems", "system_instance", subjectId, dependency.ApplicationId,
                    dependency.SourceRevision, relationship);
                Add("systems", "application", applicationId, null,
                    dependency.ApplicationRevision, relationship);
                Add("scope", "application", applicationId, null,
                    dependency.ApplicationRevision, relationship);
                Add("scope", "system_instance", subjectId, dependency.ApplicationId,
                    dependency.SourceRevision, relationship);
            }
        }

        if (content.SourceCitation?.ArtifactId is { } providerArtifactId)
            Add("evidence", "evidence_artifact", providerArtifactId, request.ProviderId,
                null, "cited_by_provider_record");
        foreach (var dependency in dependencies)
        {
            if (dependency.SourceCitation?.ArtifactId is { } dependencyArtifactId)
                Add("evidence", "evidence_artifact", dependencyArtifactId,
                    dependency.SubjectId, null, "cited_by_provider_dependency");
        }

        if (serviceIds.Count > 0)
        {
            incomplete["systems"] ??= "provider_service_system_mapping_unavailable";
            incomplete["data"] ??= "provider_service_data_mapping_unavailable";
            incomplete["controls"] ??= "provider_service_control_mapping_unavailable";
            incomplete["evidence"] ??= "provider_service_evidence_mapping_unavailable";
        }

        try
        {
            var tenantProgramIds = await ListTenantProgramIdsAsync(request.TenantId,
                incomplete, ct).ConfigureAwait(false);
            var controlProgramIds = new HashSet<Uuid>(tenantProgramIds);
            controlProgramIds.UnionWith(programIds);
            var scopeProgramIds = new HashSet<Uuid>(tenantProgramIds);
            scopeProgramIds.UnionWith(programIds);

            ProjectionCheckpoint? inventoryFence = null;
            if (instanceIds.Count > 0)
            {
                var captured = await inventoryConsistency.CaptureAsync(request.TenantId, ct)
                    .ConfigureAwait(false);
                if (!captured.IsSuccess)
                    return Result<ProviderChangeImpactPreview>.Failure(captured.Error);
                inventoryFence = captured.Value;
            }
            await AddTechnologyImpactAsync(request.TenantId, instanceIds, records["data"],
                impactedComponentIds, impactedInformationIds, impactedDataFlowIds,
                incomplete, ct).ConfigureAwait(false);

            var providerScopedPrograms = await AddBoundaryImpactAsync(request.TenantId,
                request.ProviderId, request.EffectiveOn, scopeProgramIds, serviceIds,
                applicationIds, instanceIds, impactedComponentIds, impactedInformationIds,
                impactedDataFlowIds, records["scope"], incomplete, ct)
                .ConfigureAwait(false);

            ProjectionCheckpoint? controlFence = null;
            if (controlProgramIds.Count > 0 &&
                (applicationIds.Count > 0 || instanceIds.Count > 0))
            {
                var captured = await controlConsistency.CaptureAsync(request.TenantId, ct)
                    .ConfigureAwait(false);
                if (!captured.IsSuccess)
                    return Result<ProviderChangeImpactPreview>.Failure(captured.Error);
                controlFence = captured.Value;
            }
            var impactedControls = await FindImpactedControlsAsync(request.TenantId,
                controlProgramIds, applicationIds, instanceIds, records["controls"],
                incomplete, ct).ConfigureAwait(false);

            if (providerScopedPrograms.Count > 0 && applicationIds.Count == 0 &&
                instanceIds.Count == 0 && serviceIds.Count == 0)
            {
                incomplete["systems"] ??= "provider_scope_without_system_dependency_mapping";
                incomplete["data"] ??= "provider_scope_without_data_dependency_mapping";
                incomplete["controls"] ??= "provider_scope_without_system_control_mapping";
                incomplete["evidence"] ??= "provider_scope_without_system_evidence_mapping";
            }

            await AddEvidenceImpactAsync(request.TenantId, request.ProviderId,
                impactedControls, records["evidence"], incomplete, ct).ConfigureAwait(false);

            if (inventoryFence is { } inventoryCheckpoint)
            {
                var unchangedInventory = await inventoryConsistency.ConfirmUnchangedAndCaughtUpAsync(
                    request.TenantId, inventoryCheckpoint, ct).ConfigureAwait(false);
                if (!unchangedInventory.IsSuccess)
                    return Result<ProviderChangeImpactPreview>.Failure(unchangedInventory.Error);
            }
            if (controlFence is { } controlCheckpoint)
            {
                var unchangedControls = await controlConsistency.ConfirmUnchangedAndCaughtUpAsync(
                    request.TenantId, controlCheckpoint, ct).ConfigureAwait(false);
                if (!unchangedControls.IsSuccess)
                    return Result<ProviderChangeImpactPreview>.Failure(unchangedControls.Error);
            }

            var unchanged = await programConsistency.ConfirmUnchangedAndCaughtUpAsync(
                request.TenantId, programFence.Value, ct).ConfigureAwait(false);
            if (!unchanged.IsSuccess)
                return Result<ProviderChangeImpactPreview>.Failure(unchanged.Error);

            var latestProvider = (await reader.HydrateAsync(new ProviderRegister(request.TenantId), ct)
                .ConfigureAwait(false)).Get(request.ProviderId);
            if (latestProvider is null)
                return Failure(RequestErrorKind.NotFound, "The provider was not found.");
            if (latestProvider.Revision != provider.Revision)
                return Result<ProviderChangeImpactPreview>.Failure(
                    VersionedRecordRules.StaleRevision("provider", latestProvider.Revision)
                        .ToRequestError());
            var latestResolved = await references.ResolveAsync(request.TenantId,
                latestProvider.Content, ct).ConfigureAwait(false);
            if (!latestResolved.IsSuccess)
                return Result<ProviderChangeImpactPreview>.Failure(latestResolved.Error);
            if (!SameDependencyFence(resolved.Value, latestResolved.Value))
                return Result<ProviderChangeImpactPreview>.Failure(new RequestError(
                    RequestErrorKind.Conflict,
                    "A provider dependency changed during impact assessment. Retry the preview.",
                    isTransient: true));
        }
        catch (ProviderImpactReadException exception)
        {
            return Result<ProviderChangeImpactPreview>.Failure(exception.Error);
        }

        var contexts = ContextNames.Select(name => new ProviderChangeImpactSection(name,
            records[name].OrderBy(static item => item.RecordType, StringComparer.Ordinal)
                .ThenBy(static item => item.RecordId.ToString(), StringComparer.Ordinal)
                .ThenBy(static item => item.ParentRecordId?.ToString(), StringComparer.Ordinal)
                .ToArray(), incomplete[name] is null, incomplete[name])).ToArray();
        var pending = contexts.Where(static item => !item.Complete)
            .Select(static item => item.Context).ToArray();
        var preview = new ProviderChangeImpactPreview(request.TenantId, request.ProviderId,
            provider.Revision, request.ChangeKind, request.EffectiveOn,
            request.ChangeSummary.Trim(), contexts, pending, pending.Length == 0, string.Empty);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(preview,
            ComplianceCoreJsonContext.Default.ProviderChangeImpactPreview);
        return Result<ProviderChangeImpactPreview>.Success(preview with
        {
            Digest = Convert.ToHexString(SHA256.HashData(bytes)),
        });

        void Add(string context, string recordType, Uuid recordId, Uuid? parentRecordId,
            long? revision, string relationship)
        {
            if (recordId == Uuid.Empty)
            {
                incomplete[context] ??= "invalid_source_record";
                return;
            }
            var target = records[context];
            if (target.Any(item => item.RecordType == recordType && item.RecordId == recordId &&
                                   item.ParentRecordId == parentRecordId))
                return;
            if (target.Count >= MaximumReturnedRecords)
            {
                incomplete[context] ??= "impact_record_limit";
                return;
            }
            target.Add(new ProviderChangeAffectedRecord(recordType, recordId,
                parentRecordId, revision, relationship));
        }
    }

    async ValueTask<HashSet<Uuid>> ListTenantProgramIdsAsync(Uuid tenantId,
        Dictionary<string, string?> incomplete, CancellationToken ct)
    {
        var result = new HashSet<Uuid>();
        var scanned = 0;
        string? cursor = null;
        do
        {
            var page = await programs.ListAsync(tenantId, PageSize, cursor, ct)
                .ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                if (scanned++ >= MaximumScannedRecords)
                {
                    incomplete["scope"] ??= "program_scan_limit";
                    incomplete["controls"] ??= "program_scan_limit";
                    incomplete["evidence"] ??= "program_scan_limit";
                    cursor = null;
                    break;
                }
                if (item.TenantId != tenantId || item.ProgramId == Uuid.Empty)
                {
                    incomplete["scope"] ??= "program_projection_inconsistent";
                    incomplete["controls"] ??= "program_projection_inconsistent";
                    incomplete["evidence"] ??= "program_projection_inconsistent";
                    continue;
                }
                result.Add(item.ProgramId);
            }
            if (scanned >= MaximumScannedRecords && page.NextCursor is not null)
            {
                incomplete["scope"] ??= "program_scan_limit";
                incomplete["controls"] ??= "program_scan_limit";
                incomplete["evidence"] ??= "program_scan_limit";
                cursor = null;
            }
            else
                cursor = page.NextCursor;
        } while (cursor is not null);
        return result;
    }

    async ValueTask AddTechnologyImpactAsync(Uuid tenantId, HashSet<Uuid> instanceIds,
        List<ProviderChangeAffectedRecord> records, HashSet<Uuid> impactedComponentIds,
        HashSet<Uuid> impactedInformationIds, HashSet<Uuid> impactedDataFlowIds,
        Dictionary<string, string?> incomplete, CancellationToken ct)
    {
        if (instanceIds.Count == 0)
            return;

        var components = new Dictionary<Uuid, TechnologyComponentView>();
        var scanned = 0;
        string? cursor = null;
        do
        {
            var page = await inventory.ListComponentsAsync(tenantId, PageSize, cursor, ct)
                .ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                if (scanned++ >= MaximumScannedRecords)
                {
                    incomplete["data"] ??= "inventory_scan_limit";
                    cursor = null;
                    break;
                }
                if (item.TenantId != tenantId || item.ComponentId == Uuid.Empty)
                {
                    incomplete["data"] ??= "inventory_projection_inconsistent";
                    continue;
                }
                if (item.Content.SystemInstanceId is not { } systemInstanceId ||
                    !instanceIds.Contains(systemInstanceId))
                    continue;
                var current = await inventoryConsistency.GetComponentAsync(tenantId,
                    item.ComponentId, item.Revision, ct).ConfigureAwait(false);
                if (!current.IsSuccess)
                    throw new ProviderImpactReadException(current.Error);
                if (current.Value.Content.SystemInstanceId is not { } currentSystemInstanceId ||
                    !instanceIds.Contains(currentSystemInstanceId))
                    continue;
                components.TryAdd(current.Value.ComponentId, current.Value);
                AddScopeId(impactedComponentIds, current.Value.ComponentId);
                Add("technology_component", current.Value.ComponentId, currentSystemInstanceId,
                    current.Value.Revision, "technology_component_belongs_to_affected_system");
            }
            if (scanned >= MaximumScannedRecords && page.NextCursor is not null)
            {
                incomplete["data"] ??= "inventory_scan_limit";
                cursor = null;
            }
            else
                cursor = page.NextCursor;
        } while (cursor is not null);

        var componentIds = components.Keys.ToHashSet();
        scanned = 0;
        cursor = null;
        do
        {
            var page = await inventory.ListFlowsAsync(tenantId, PageSize, cursor, ct)
                .ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                if (scanned++ >= MaximumScannedRecords)
                {
                    incomplete["data"] ??= "inventory_scan_limit";
                    cursor = null;
                    break;
                }
                if (item.TenantId != tenantId || item.DataFlowId == Uuid.Empty)
                {
                    incomplete["data"] ??= "inventory_projection_inconsistent";
                    continue;
                }
                var sourceMatches = item.Content.SourceType == TechnologyInventoryRules.SystemInstanceReference
                    ? instanceIds.Contains(item.Content.SourceId)
                    : item.Content.SourceType == TechnologyInventoryRules.ComponentReference &&
                      componentIds.Contains(item.Content.SourceId);
                var destinationMatches = item.Content.DestinationType == TechnologyInventoryRules.ComponentReference &&
                                         item.Content.DestinationId is { } destinationId &&
                                         componentIds.Contains(destinationId);
                if (!sourceMatches && !destinationMatches)
                    continue;
                var current = await inventoryConsistency.GetFlowAsync(tenantId,
                    item.DataFlowId, item.Revision, ct).ConfigureAwait(false);
                if (!current.IsSuccess)
                    throw new ProviderImpactReadException(current.Error);
                var currentContent = current.Value.Content;
                var currentSourceMatches = currentContent.SourceType == TechnologyInventoryRules.SystemInstanceReference
                    ? instanceIds.Contains(currentContent.SourceId)
                    : currentContent.SourceType == TechnologyInventoryRules.ComponentReference &&
                      componentIds.Contains(currentContent.SourceId);
                var currentDestinationMatches = currentContent.DestinationType == TechnologyInventoryRules.ComponentReference &&
                                                currentContent.DestinationId is { } currentDestinationId &&
                                                componentIds.Contains(currentDestinationId);
                if (!currentSourceMatches && !currentDestinationMatches)
                    continue;
                AddScopeId(impactedDataFlowIds, current.Value.DataFlowId);
                Add("data_flow", current.Value.DataFlowId, null, current.Value.Revision,
                    "data_flow_touches_affected_system");
                foreach (var assetId in current.Value.Content.InformationAssetIds)
                {
                    var asset = await inventoryConsistency.GetAssetAsync(tenantId, assetId,
                        null, ct).ConfigureAwait(false);
                    if (!asset.IsSuccess)
                    {
                        incomplete["data"] ??= "information_asset_reference_unavailable";
                        continue;
                    }
                    AddScopeId(impactedInformationIds, asset.Value.InformationAssetId);
                    Add("information_asset", asset.Value.InformationAssetId,
                        current.Value.DataFlowId, asset.Value.Revision,
                        "carried_by_affected_data_flow");
                }
            }
            if (scanned >= MaximumScannedRecords && page.NextCursor is not null)
            {
                incomplete["data"] ??= "inventory_scan_limit";
                cursor = null;
            }
            else
                cursor = page.NextCursor;
        } while (cursor is not null);

        void Add(string recordType, Uuid recordId, Uuid? parentId, long? revision,
            string relationship)
        {
            if (records.Any(item => item.RecordType == recordType && item.RecordId == recordId &&
                                    item.ParentRecordId == parentId))
                return;
            if (records.Count >= MaximumReturnedRecords)
            {
                incomplete["data"] ??= "impact_record_limit";
                return;
            }
            records.Add(new ProviderChangeAffectedRecord(recordType, recordId, parentId,
                revision, relationship));
        }

        void AddScopeId(HashSet<Uuid> target, Uuid recordId)
        {
            if (target.Contains(recordId))
                return;
            if (target.Count >= MaximumScannedRecords)
            {
                incomplete["data"] ??= "impact_reference_limit";
                incomplete["scope"] ??= "impact_reference_limit";
                return;
            }
            target.Add(recordId);
        }
    }

    async ValueTask<HashSet<(Uuid ProgramId, Uuid ControlId)>> FindImpactedControlsAsync(Uuid tenantId,
        HashSet<Uuid> programIds, HashSet<Uuid> applicationIds, HashSet<Uuid> instanceIds,
        List<ProviderChangeAffectedRecord> records, Dictionary<string, string?> incomplete,
        CancellationToken ct)
    {
        var result = new HashSet<(Uuid ProgramId, Uuid ControlId)>();
        if (programIds.Count == 0 || applicationIds.Count == 0 && instanceIds.Count == 0)
            return result;
        var caughtUp = await controlConsistency.EnsureCaughtUpAsync(tenantId, ct)
            .ConfigureAwait(false);
        if (!caughtUp.IsSuccess)
            throw new ProviderImpactReadException(caughtUp.Error);

        var scanned = 0;
        var scanLimitReached = false;
        foreach (var programId in programIds.OrderBy(static id => id.ToString(), StringComparer.Ordinal))
        {
            if (scanLimitReached)
                break;
            string? cursor = null;
            do
            {
                var page = await controls.ListProgramAsync(tenantId, programId, PageSize,
                    cursor, ct).ConfigureAwait(false);
                foreach (var item in page.Items)
                {
                    if (scanned++ >= MaximumScannedRecords)
                    {
                        incomplete["controls"] ??= "control_scan_limit";
                        scanLimitReached = true;
                        cursor = null;
                        break;
                    }
                    if (item.TenantId != tenantId || item.ProgramId != programId ||
                        item.ControlId == Uuid.Empty)
                    {
                        incomplete["controls"] ??= "control_projection_inconsistent";
                        continue;
                    }
                    var control = await reader.HydrateAsync(new ControlDraft(tenantId,
                        item.ControlId), ct).ConfigureAwait(false);
                    if (!control.IsVisible || control.TenantId != tenantId ||
                        control.ProgramId != programId)
                    {
                        incomplete["controls"] ??= "control_source_projection_mismatch";
                        continue;
                    }
                    if (!ReferencesAffectedSystem(control, applicationIds, instanceIds))
                        continue;
                    result.Add((programId, control.Id));
                    if (records.Count < MaximumReturnedRecords)
                        records.Add(new ProviderChangeAffectedRecord("control", control.Id,
                            programId, control.Revision,
                            "control_applicability_references_affected_system"));
                    else
                        incomplete["controls"] ??= "impact_record_limit";
                }
                if (scanned >= MaximumScannedRecords && page.NextCursor is not null)
                {
                    incomplete["controls"] ??= "control_scan_limit";
                    scanLimitReached = true;
                    cursor = null;
                }
                else
                    cursor = page.NextCursor;
            } while (cursor is not null);
        }
        return result;
    }

    async ValueTask AddEvidenceImpactAsync(Uuid tenantId, Uuid providerId,
        HashSet<(Uuid ProgramId, Uuid ControlId)> controls,
        List<ProviderChangeAffectedRecord> records,
        Dictionary<string, string?> incomplete, CancellationToken ct)
    {
        var assurance = await reader.HydrateAsync(new ProviderAssuranceRegister(tenantId), ct)
            .ConfigureAwait(false);
        var reportSnapshot = assurance.Reports(providerId);
        var reviewSnapshot = assurance.Reviews(providerId);
        foreach (var report in reportSnapshot)
        {
            Add("assurance_report", report.ReportId, null, report.Revision,
                "provider_assurance_record");
            if (report.Content.Citation?.ArtifactId is { } artifactId)
                Add("evidence_artifact", artifactId, report.ReportId, null,
                    "cited_by_provider_assurance_report");
        }
        foreach (var review in reviewSnapshot)
        {
            Add("provider_review", review.ReviewId, null, null,
                "provider_due_diligence_record");
            if (review.Content.AssuranceReportId is { } reportId)
                Add("assurance_report", reportId, review.ReviewId, null,
                    "reviewed_provider_assurance_report");
            if (review.Content.Evidence?.ArtifactId is { } artifactId)
                Add("evidence_artifact", artifactId, review.ReviewId, null,
                    "cited_by_provider_due_diligence_review");
        }

        var scanned = 0;
        var scanLimitReached = false;
        var ledgerSnapshots = new Dictionary<Uuid, IReadOnlyList<EvidenceRequestView>>();
        foreach (var programId in controls.Select(static item => item.ProgramId).Distinct())
        {
            if (scanLimitReached)
                break;
            var ledger = await reader.HydrateAsync(new EvidenceRequestLedger(tenantId,
                programId), ct).ConfigureAwait(false);
            var snapshot = ledger.ReadAll();
            ledgerSnapshots.Add(programId, snapshot);
            foreach (var item in snapshot)
            {
                if (scanned++ >= MaximumScannedRecords)
                {
                    incomplete["evidence"] ??= "evidence_scan_limit";
                    scanLimitReached = true;
                    break;
                }
                if (item.TenantId != tenantId || item.ProgramId != programId)
                {
                    incomplete["evidence"] ??= "evidence_source_mismatch";
                    continue;
                }
                if (item.ControlId is not { } controlId ||
                    !controls.Contains((programId, controlId)))
                    continue;
                Add("evidence_request", item.EvidenceRequestId, controlId,
                    item.Revision, "request_attached_to_affected_control");
                if (item.ArtifactId is { } artifactId)
                    Add("evidence_artifact", artifactId, item.EvidenceRequestId,
                        null, "fulfills_request_for_affected_control");
            }
        }

        var currentAssurance = await reader.HydrateAsync(new ProviderAssuranceRegister(tenantId), ct)
            .ConfigureAwait(false);
        if (!reportSnapshot.SequenceEqual(currentAssurance.Reports(providerId)) ||
            !reviewSnapshot.SequenceEqual(currentAssurance.Reviews(providerId)))
            throw new ProviderImpactReadException(SourceChangedError("provider assurance evidence"));
        foreach (var (programId, snapshot) in ledgerSnapshots)
        {
            var currentLedger = await reader.HydrateAsync(new EvidenceRequestLedger(tenantId,
                programId), ct).ConfigureAwait(false);
            if (!snapshot.SequenceEqual(currentLedger.ReadAll()))
                throw new ProviderImpactReadException(SourceChangedError("evidence requests"));
        }

        void Add(string recordType, Uuid recordId, Uuid? parentId, long? revision,
            string relationship)
        {
            if (records.Any(item => item.RecordType == recordType && item.RecordId == recordId &&
                                    item.ParentRecordId == parentId))
                return;
            if (records.Count >= MaximumReturnedRecords)
            {
                incomplete["evidence"] ??= "impact_record_limit";
                return;
            }
            records.Add(new ProviderChangeAffectedRecord(recordType, recordId, parentId,
                revision, relationship));
        }
    }

    async ValueTask<HashSet<Uuid>> AddBoundaryImpactAsync(Uuid tenantId, Uuid providerId,
        DateOnly effectiveOn, HashSet<Uuid> programIds, HashSet<Uuid> serviceIds,
        HashSet<Uuid> applicationIds, HashSet<Uuid> instanceIds,
        HashSet<Uuid> componentIds, HashSet<Uuid> informationIds,
        HashSet<Uuid> dataFlowIds,
        List<ProviderChangeAffectedRecord> records, Dictionary<string, string?> incomplete,
        CancellationToken ct)
    {
        var providerScopedPrograms = new HashSet<Uuid>();
        var observedBoundaries = new Dictionary<Uuid, (Uuid ProgramId, long Revision)>();
        var scanned = 0;
        var scanLimitReached = false;
        foreach (var programId in programIds.OrderBy(static id => id.ToString(), StringComparer.Ordinal))
        {
            if (scanLimitReached)
                break;
            string? cursor = null;
            do
            {
                var page = await boundaries.ListProgramAsync(tenantId, programId, PageSize,
                    cursor, ct).ConfigureAwait(false);
                foreach (var boundary in page.Items)
                {
                    if (scanned++ >= MaximumScannedRecords)
                    {
                        incomplete["scope"] ??= "boundary_scan_limit";
                        scanLimitReached = true;
                        cursor = null;
                        break;
                    }
                    if (boundary.TenantId != tenantId || boundary.ProgramId != programId ||
                        boundary.BoundaryId == Uuid.Empty)
                    {
                        incomplete["scope"] ??= "boundary_projection_inconsistent";
                        continue;
                    }
                    var source = await reader.HydrateAsync(new SystemBoundary(tenantId,
                        boundary.BoundaryId), ct).ConfigureAwait(false);
                    if (!source.IsCreated || source.ProgramId != boundary.ProgramId ||
                        source.Revision != boundary.Revision)
                    {
                        incomplete["scope"] ??= "boundary_source_projection_mismatch";
                        continue;
                    }
                    observedBoundaries[boundary.BoundaryId] = (boundary.ProgramId,
                        boundary.Revision);
                    var effectiveVersion = await boundaries.GetEffectiveVersionAsync(tenantId,
                        boundary.BoundaryId, effectiveOn, ct).ConfigureAwait(false);
                    var versions = new[] { effectiveVersion, boundary.Draft }
                        .Where(static version => version is not null)
                        .DistinctBy(static version => version!.VersionId);
                    foreach (var version in versions)
                    {
                        foreach (var entry in version!.Content.Entries)
                        {
                            if (!ReferencesAffectedScope(entry, providerId, serviceIds,
                                    applicationIds, instanceIds, componentIds,
                                    informationIds, dataFlowIds))
                                continue;
                            var isEffectiveVersion = effectiveVersion?.VersionId == version.VersionId;
                            Add("boundary_scope_entry", entry.EntryId, boundary.BoundaryId,
                                version.Revision, isEffectiveVersion
                                    ? "effective_boundary_scope_entry"
                                    : "draft_boundary_scope_entry");
                            Add("boundary_version", version.VersionId, boundary.BoundaryId,
                                version.Revision, isEffectiveVersion
                                    ? "effective_boundary_version"
                                    : "draft_boundary_version");
                            if (entry.SubjectType == "provider" &&
                                entry.GovernedRecordId == providerId)
                                providerScopedPrograms.Add(programId);
                        }
                    }
                }
                if (scanned >= MaximumScannedRecords && page.NextCursor is not null)
                {
                    incomplete["scope"] ??= "boundary_scan_limit";
                    scanLimitReached = true;
                    cursor = null;
                }
                else
                    cursor = page.NextCursor;
            } while (cursor is not null);
        }

        foreach (var (boundaryId, observation) in observedBoundaries)
        {
            var source = await reader.HydrateAsync(new SystemBoundary(tenantId, boundaryId), ct)
                .ConfigureAwait(false);
            var view = await boundaries.GetAsync(tenantId, boundaryId, ct).ConfigureAwait(false);
            if (!source.IsCreated || source.ProgramId != observation.ProgramId ||
                source.Revision != observation.Revision || view is null ||
                view.TenantId != tenantId || view.BoundaryId != boundaryId ||
                view.ProgramId != observation.ProgramId || view.Revision != observation.Revision)
                incomplete["scope"] ??= "boundary_source_projection_changed_during_scan";
        }
        return providerScopedPrograms;

        void Add(string recordType, Uuid recordId, Uuid? parentId, long? revision,
            string relationship)
        {
            if (records.Any(item => item.RecordType == recordType && item.RecordId == recordId &&
                                    item.ParentRecordId == parentId))
                return;
            if (records.Count >= MaximumReturnedRecords)
            {
                incomplete["scope"] ??= "impact_record_limit";
                return;
            }
            records.Add(new ProviderChangeAffectedRecord(recordType, recordId, parentId,
                revision, relationship));
        }
    }

    static bool ReferencesAffectedSystem(ControlDraft control, HashSet<Uuid> applicationIds,
        HashSet<Uuid> instanceIds) =>
        ReferencesAffectedSystem(control.ApprovedVersion?.Content, applicationIds, instanceIds) ||
        ReferencesAffectedSystem(control.CurrentContent, applicationIds, instanceIds);

    static bool ReferencesAffectedSystem(ControlDraftContent? content,
        HashSet<Uuid> applicationIds, HashSet<Uuid> instanceIds) =>
        (content?.Applicability ?? []).Any(reference =>
            reference is { Unresolved: false, GovernedRecordId: { } id }
            && id != Uuid.Empty
            && ((reference.SubjectType == "application" && applicationIds.Contains(id)) ||
                (reference.SubjectType == "system_instance" && instanceIds.Contains(id))));

    static bool ReferencesAffectedScope(BoundaryScopeEntry entry, Uuid providerId,
        HashSet<Uuid> serviceIds, HashSet<Uuid> applicationIds, HashSet<Uuid> instanceIds,
        HashSet<Uuid> componentIds, HashSet<Uuid> informationIds, HashSet<Uuid> dataFlowIds) =>
        entry is { Unresolved: false, GovernedRecordId: { } id } && id != Uuid.Empty &&
        (entry.SubjectType == "provider" && id == providerId ||
         entry.SubjectType == "service" && serviceIds.Contains(id) ||
         entry.SubjectType == "application" && applicationIds.Contains(id) ||
         entry.SubjectType == "system_instance" && instanceIds.Contains(id) ||
         entry.SubjectType == "component" && componentIds.Contains(id) ||
         entry.SubjectType == "information" && informationIds.Contains(id) ||
         entry.SubjectType == "data_flow" && dataFlowIds.Contains(id));

    static bool IsAffectedByChange(ProviderDependency dependency, string changeKind,
        DateOnly effectiveOn)
    {
        var date = new DateTimeOffset(effectiveOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (changeKind == "termination")
            return dependency.EffectiveFrom < date &&
                   (dependency.EffectiveUntilExclusive is null ||
                    date <= dependency.EffectiveUntilExclusive);
        return dependency.EffectiveFrom <= date &&
               (dependency.EffectiveUntilExclusive is null || date < dependency.EffectiveUntilExclusive);
    }

    static string DependencyRelationship(string changeKind, bool affected) =>
        changeKind == "termination"
            ? affected ? "provider_dependency_active_before_termination" :
                "provider_dependency_outside_termination_window"
            : affected ? "provider_dependency_effective_on_change_date" :
                "provider_dependency_outside_change_date";

    static bool SameDependencyFence(ProviderContent before, ProviderContent after)
    {
        if (before.OwnerPersonId != after.OwnerPersonId ||
            before.OwnerPersonRevision != after.OwnerPersonRevision)
            return false;
        var beforeDependencies = before.Dependencies ?? [];
        var afterDependencies = after.Dependencies ?? [];
        return beforeDependencies.Count == afterDependencies.Count &&
               beforeDependencies.Zip(afterDependencies).All(static pair =>
                   pair.First.SubjectKind == pair.Second.SubjectKind &&
                   pair.First.SubjectId == pair.Second.SubjectId &&
                   pair.First.ProgramId == pair.Second.ProgramId &&
                   pair.First.ApplicationId == pair.Second.ApplicationId &&
                   pair.First.SourceRevision == pair.Second.SourceRevision &&
                   pair.First.ProgramRevision == pair.Second.ProgramRevision &&
                   pair.First.ApplicationRevision == pair.Second.ApplicationRevision);
    }

    static Result<ProviderChangeImpactPreview> Failure(RequestErrorKind kind, string message) =>
        Result<ProviderChangeImpactPreview>.Failure(new RequestError(kind, message));

    static RequestError SourceChangedError(string source) => new(RequestErrorKind.Conflict,
        $"The {source} changed during impact assessment. Retry the preview.", isTransient: true);
}

sealed class ProviderImpactReadException(RequestError error) : Exception(error.Message)
{
    public RequestError Error { get; } = error;
}
